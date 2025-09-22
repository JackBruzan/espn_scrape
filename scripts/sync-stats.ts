/**
 * ESPN NFL Stats Sync Service
 * TypeScript implementation for syncing player statistics only
 */

// Configuration
interface SyncConfig {
    espnApiBaseUrl: string;
    supabaseUrl: string;
    supabaseKey: string;
    season: number;
    startWeek: number;
    endWeek: number;
    seasonType: number; // 1=preseason, 2=regular, 3=postseason
}

// ESPN Data Models
interface EspnEvent {
    id: string;
    name: string;
    date: string;
    status: {
        type: {
            completed: boolean;
        };
    };
    competitions: Array<{
        id: string;
        competitors: Array<{
            id: string;
            team: {
                id: string;
                abbreviation: string;
                displayName: string;
            };
        }>;
    }>;
}

interface EspnPlayerStats {
    playerId: string;
    displayName: string;
    shortName: string;
    team: {
        id: string;
        abbreviation: string;
        displayName: string;
    };
    position: {
        abbreviation: string;
        displayName: string;
    };
    jersey: string;
    statistics: Array<{
        name: string;
        displayName: string;
        value: string | number;
        displayValue: string;
        category: string;
    }>;
    gameId: string;
    season: number;
    week: number;
    seasonType: number;
}

// Database Models
interface PlayerStatRecord {
    player_id: string;
    game_id: string;
    season: number;
    week: number;
    season_type: number;
    stat_name: string;
    stat_value: number;
    stat_display_value: string;
    stat_category: string;
    team_abbreviation: string;
    position: string;
    created_at?: string;
    updated_at?: string;
}

// Simple Supabase client interface
interface SupabaseClient {
    from(table: string): {
        upsert(data: any[], options?: { onConflict?: string; ignoreDuplicates?: boolean }): {
            select(columns?: string): Promise<{ data: any[] | null; error: any }>;
        };
    };
}

class EspnStatsSync {
    private config: SyncConfig;
    private supabase: SupabaseClient;

    // Stat category mappings
    private statCategoryMappings = new Map<string, string>([
        // Passing stats
        ['passingCompletions', 'passing'],
        ['passingAttempts', 'passing'],
        ['passingYards', 'passing'],
        ['passingTouchdowns', 'passing'],
        ['passingInterceptions', 'passing'],
        ['passingRating', 'passing'],
        ['passingQBR', 'passing'],
        ['completions', 'passing'],
        ['attempts', 'passing'],
        ['comp-att', 'passing'],
        ['C/ATT', 'passing'],

        // Rushing stats
        ['rushingCarries', 'rushing'],
        ['rushingYards', 'rushing'],
        ['rushingTouchdowns', 'rushing'],
        ['rushingAverage', 'rushing'],
        ['rushingLong', 'rushing'],
        ['carries', 'rushing'],

        // Receiving stats
        ['receivingReceptions', 'receiving'],
        ['receivingTargets', 'receiving'],
        ['receivingYards', 'receiving'],
        ['receivingTouchdowns', 'receiving'],
        ['receivingAverage', 'receiving'],
        ['receivingLong', 'receiving'],
        ['receptions', 'receiving'],
        ['targets', 'receiving'],

        // Defensive stats
        ['totalTackles', 'defensive'],
        ['soloTackles', 'defensive'],
        ['assistTackles', 'defensive'],
        ['sacks', 'defensive'],
        ['interceptions', 'defensive'],
        ['passesDefended', 'defensive'],
        ['forcedFumbles', 'defensive'],
        ['fumbleRecoveries', 'defensive'],
        ['defensiveTouchdowns', 'defensive'],

        // Kicking stats
        ['fieldGoalsMade', 'kicking'],
        ['fieldGoalsAttempted', 'kicking'],
        ['extraPointsMade', 'kicking'],
        ['extraPointsAttempted', 'kicking'],
        ['fieldGoals', 'kicking'],
        ['extraPoints', 'kicking'],

        // Punting stats
        ['punts', 'punting'],
        ['puntingYards', 'punting'],
        ['puntingAverage', 'punting'],
        ['puntingLong', 'punting'],
        ['puntingInside20', 'punting']
    ]);

    // Team abbreviation mappings (ESPN -> Database)
    private teamMappings = new Map<string, string>([
        ['KC', 'KAN'],
        ['NE', 'NWE'],
        ['SF', 'SFO'],
        ['TB', 'TAM'],
        ['LV', 'LVR'],
        ['NO', 'NOR']
    ]);

    constructor(config: SyncConfig, supabaseClient: SupabaseClient) {
        this.config = config;
        this.supabase = supabaseClient;
    }

    /**
     * Main sync method - syncs stats for specified weeks
     */
    async syncStats(): Promise<void> {
        console.log(`Starting stats sync for ${this.config.season} season, weeks ${this.config.startWeek}-${this.config.endWeek}`);

        let totalGamesProcessed = 0;
        let totalStatsInserted = 0;

        try {
            for (let week = this.config.startWeek; week <= this.config.endWeek; week++) {
                console.log(`Processing week ${week}...`);

                const events = await this.getWeekEvents(week);
                console.log(`Found ${events.length} games for week ${week}`);

                for (const event of events) {
                    if (event.status.type.completed) {
                        const playerStats = await this.extractGameStats(event.id);
                        if (playerStats.length > 0) {
                            const insertedCount = await this.saveStats(playerStats);
                            totalStatsInserted += insertedCount;
                            console.log(`Saved ${insertedCount} stats for game ${event.id} (${event.name})`);
                        }
                        totalGamesProcessed++;
                    } else {
                        console.log(`Skipping incomplete game: ${event.name}`);
                    }
                }

                // Add delay between weeks to avoid rate limiting
                await this.sleep(1000);
            }

            console.log(`Sync completed! Processed ${totalGamesProcessed} games, inserted ${totalStatsInserted} stats`);

        } catch (error) {
            console.error('Error during stats sync:', error);
            throw error;
        }
    }

    /**
     * Get all events (games) for a specific week
     */
    private async getWeekEvents(week: number): Promise<EspnEvent[]> {
        const url = `${this.config.espnApiBaseUrl}/nfl/scoreboard/_/week/${week}/year/${this.config.season}/seasontype/${this.config.seasonType}`;

        try {
            const response = await fetch(url);
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const html = await response.text();
            const jsonMatch = html.match(/window\['__espnfitt__'\]\s*=\s*({.*?});/);

            if (!jsonMatch) {
                console.warn(`No ESPN data found for week ${week}`);
                return [];
            }

            const data = JSON.parse(jsonMatch[1]);
            const events = data?.page?.content?.scoreboard?.evts || [];

            return events.map((event: any) => ({
                id: event.id,
                name: event.name,
                date: event.date,
                status: event.status,
                competitions: event.competitions || []
            }));

        } catch (error) {
            console.error(`Error fetching week ${week} events:`, error);
            return [];
        }
    }

    /**
     * Extract player stats from a specific game
     */
    private async extractGameStats(gameId: string): Promise<EspnPlayerStats[]> {
        const url = `${this.config.espnApiBaseUrl}/nfl/boxscore/_/gameId/${gameId}`;

        try {
            const response = await fetch(url);
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const html = await response.text();
            const jsonMatch = html.match(/window\['__espnfitt__'\]\s*=\s*({.*?});/);

            if (!jsonMatch) {
                console.warn(`No box score data found for game ${gameId}`);
                return [];
            }

            const data = JSON.parse(jsonMatch[1]);
            return this.parsePlayerStats(data, gameId);

        } catch (error) {
            console.error(`Error extracting stats for game ${gameId}:`, error);
            return [];
        }
    }

    /**
     * Parse player statistics from ESPN box score data
     */
    private parsePlayerStats(data: any, gameId: string): EspnPlayerStats[] {
        const playerStats: EspnPlayerStats[] = [];

        try {
            const boxscore = data?.page?.content?.boxscore;
            if (!boxscore) return playerStats;

            // Extract players from both teams
            const players = boxscore.players || [];

            for (const teamPlayers of players) {
                const team = teamPlayers.team || {};
                const statistics = teamPlayers.statistics || [];

                for (const statCategory of statistics) {
                    const athletes = statCategory.athletes || [];

                    for (const athlete of athletes) {
                        const player = athlete.athlete || {};
                        const stats = athlete.stats || [];

                        if (player.id && stats.length > 0) {
                            const playerStat: EspnPlayerStats = {
                                playerId: player.id,
                                displayName: player.displayName || '',
                                shortName: player.shortName || '',
                                team: {
                                    id: team.id || '',
                                    abbreviation: this.mapTeamAbbreviation(team.abbreviation || ''),
                                    displayName: team.displayName || ''
                                },
                                position: {
                                    abbreviation: player.position?.abbreviation || '',
                                    displayName: player.position?.displayName || ''
                                },
                                jersey: player.jersey || '',
                                statistics: this.parseStatistics(stats, statCategory.name),
                                gameId,
                                season: this.config.season,
                                week: this.getCurrentWeek(gameId),
                                seasonType: this.config.seasonType
                            };

                            playerStats.push(playerStat);
                        }
                    }
                }
            }

        } catch (error) {
            console.error(`Error parsing player stats for game ${gameId}:`, error);
        }

        return playerStats;
    }

    /**
     * Parse individual statistics with proper categorization
     */
    private parseStatistics(stats: any[], categoryName: string): Array<{
        name: string;
        displayName: string;
        value: string | number;
        displayValue: string;
        category: string;
    }> {
        return stats.map((stat, index) => ({
            name: stat.name || `stat_${index}`,
            displayName: stat.displayName || stat.name || `Stat ${index + 1}`,
            value: this.parseStatValue(stat.value),
            displayValue: stat.displayValue || String(stat.value || ''),
            category: this.getStatCategory(stat.name || categoryName)
        }));
    }

    /**
     * Parse stat value handling different data types
     */
    private parseStatValue(value: any): string | number {
        if (typeof value === 'number') return value;
        if (typeof value === 'string') {
            const numValue = parseFloat(value);
            return isNaN(numValue) ? value : numValue;
        }
        return String(value || '');
    }

    /**
     * Get stat category from stat name
     */
    private getStatCategory(statName: string): string {
        const normalized = statName.toLowerCase().replace(/[^a-z]/g, '');
        return this.statCategoryMappings.get(normalized) || 'general';
    }

    /**
     * Map ESPN team abbreviations to database format
     */
    private mapTeamAbbreviation(espnAbbrev: string): string {
        return this.teamMappings.get(espnAbbrev) || espnAbbrev;
    }

    /**
     * Get current week from game context (simplified)
     */
    private getCurrentWeek(gameId: string): number {
        // In a real implementation, you'd extract week from game metadata
        // For now, return the current week being processed
        return this.config.startWeek;
    }

    /**
     * Save player stats to Supabase
     */
    private async saveStats(playerStats: EspnPlayerStats[]): Promise<number> {
        const records: PlayerStatRecord[] = [];

        for (const player of playerStats) {
            for (const stat of player.statistics) {
                const record: PlayerStatRecord = {
                    player_id: player.playerId,
                    game_id: player.gameId,
                    season: player.season,
                    week: player.week,
                    season_type: player.seasonType,
                    stat_name: stat.name,
                    stat_value: typeof stat.value === 'number' ? stat.value : parseFloat(String(stat.value)) || 0,
                    stat_display_value: stat.displayValue,
                    stat_category: stat.category || 'general',
                    team_abbreviation: player.team.abbreviation,
                    position: player.position.abbreviation
                };

                records.push(record);
            }
        }

        if (records.length === 0) return 0;

        try {
            // Use upsert to handle duplicates
            const { data, error } = await this.supabase
                .from('player_stats')
                .upsert(records, {
                    onConflict: 'player_id,game_id,stat_name',
                    ignoreDuplicates: false
                })
                .select('id');

            if (error) {
                console.error('Error saving stats:', error);
                throw error;
            }

            return data?.length || 0;

        } catch (error) {
            console.error('Failed to save stats:', error);
            throw error;
        }
    }

    /**
     * Utility function for delays
     */
    private sleep(ms: number): Promise<void> {
        return new Promise(resolve => setTimeout(resolve, ms));
    }
}

// Usage example
async function main() {
    // Configuration - replace with your actual values
    const config: SyncConfig = {
        espnApiBaseUrl: 'https://www.espn.com',
        supabaseUrl: 'your-supabase-url',
        supabaseKey: 'your-supabase-key',
        season: 2024,
        startWeek: 1,
        endWeek: 18,
        seasonType: 2 // Regular season
    };

    // Mock Supabase client for demonstration
    const mockSupabaseClient: SupabaseClient = {
        from: (table: string) => ({
            upsert: (data: any[], options?: any) => ({
                select: async (columns?: string) => {
                    console.log(`Mock: Inserting ${data.length} records into ${table}`);
                    return { data: data.map((_, i) => ({ id: i })), error: null };
                }
            })
        })
    };

    const sync = new EspnStatsSync(config, mockSupabaseClient);

    try {
        await sync.syncStats();
        console.log('Stats sync completed successfully!');
    } catch (error) {
        console.error('Stats sync failed:', error);
    }
}

// Export for use as module
export { EspnStatsSync, SyncConfig, EspnPlayerStats, PlayerStatRecord };

// Example usage
// main().catch(console.error);