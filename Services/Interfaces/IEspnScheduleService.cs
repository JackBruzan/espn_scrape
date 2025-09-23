using ESPNScrape.Models;

namespace ESPNScrape.Services.Interfaces
{
    public interface IEspnScheduleService
    {
        /// <summary>
        /// Gets weekly schedule using HTML scraping (legacy method)
        /// </summary>
        Task<IEnumerable<Schedule>> GetWeeklyScheduleAsync(int year, int week, int seasonType = 2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets weekly schedule using ESPN Core API (recommended method)
        /// </summary>
        Task<IEnumerable<Schedule>> GetWeeklyScheduleFromApiAsync(int year, int week, int seasonType = 2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves schedule data to the database
        /// </summary>
        Task SaveScheduleDataAsync(IEnumerable<Schedule> schedules, CancellationToken cancellationToken = default);
    }
}