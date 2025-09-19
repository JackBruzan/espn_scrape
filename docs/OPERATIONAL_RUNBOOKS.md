# ESPN API Service - Operational Runbooks

This document provides step-by-step procedures for common operational scenarios. Use these runbooks to ensure consistent and reliable service operations.

## Table of Contents
- [Service Startup and Shutdown](#service-startup-and-shutdown)
- [Health Monitoring Procedures](#health-monitoring-procedures)
- [Alert Investigation and Response](#alert-investigation-and-response)
- [Performance Troubleshooting](#performance-troubleshooting)
- [Maintenance Procedures](#maintenance-procedures)
- [Incident Response](#incident-response)
- [Backup and Recovery](#backup-and-recovery)

---

## Service Startup and Shutdown

### 🚀 Service Startup Procedure

#### Pre-Startup Checklist
- [ ] Verify system resources (CPU, Memory, Disk space)
- [ ] Check network connectivity to ESPN API
- [ ] Validate configuration files
- [ ] Ensure logging directory is writable
- [ ] Verify scheduled job configurations

#### Startup Steps

**1. Start the Service**
```bash
# Docker deployment
docker-compose up -d espn-api-service

# Or direct Docker run
docker run -d \
  --name espn-service \
  -p 5000:80 \
  -v $(pwd)/logs:/app/logs \
  -v $(pwd)/downloads:/app/downloads \
  espn-api-service

# Or local development
dotnet run --project ESPNScrape.csproj
```

**2. Verify Initial Health**
```bash
# Wait 30 seconds for service to initialize
sleep 30

# Check health status
curl -s http://localhost:5000/health | jq '.status'
# Expected: "Healthy"
```

**3. Validate Core Components**
```bash
# Check ESPN API connectivity
curl -s http://localhost:5000/health | jq '.entries.espn_api.status'
# Expected: "Healthy"

# Verify alert monitoring started
curl -s http://localhost:5000/metrics | jq '.business.apiCallsToday'
# Expected: Number >= 0
```

**4. Monitor Startup Logs**
```bash
# Docker logs
docker logs espn-service --tail 50 -f

# Look for these success indicators:
# ✅ "Starting ESPN Scrape Service"
# ✅ "Alert monitoring service started"
# ✅ "Application started. Press Ctrl+C to shut down"
# ✅ "Now listening on: http://localhost:5000"
```

#### Post-Startup Validation

**Check All Diagnostic Endpoints**
```bash
# Health check
curl -f http://localhost:5000/health

# Metrics endpoint
curl -f http://localhost:5000/metrics

# System information
curl -f http://localhost:5000/system-info

# Configuration
curl -f http://localhost:5000/config
```

**Validate Scheduled Jobs**
```bash
# Check logs for Quartz initialization
docker logs espn-service | grep -i "quartz\|scheduler\|job"

# Expected log entries:
# "Quartz Scheduler created"
# "Adding 2 jobs, 3 triggers"
# "Scheduler QuartzScheduler started"
```

### 🛑 Service Shutdown Procedure

#### Graceful Shutdown Steps

**1. Disable New Requests** (if using load balancer)
```bash
# Remove from load balancer rotation
# (Implementation depends on your load balancer)
```

**2. Allow Current Operations to Complete**
```bash
# Monitor active operations
curl -s http://localhost:5000/metrics | jq '.performance'

# Wait for operations to finish (typically 30-60 seconds)
```

**3. Stop Scheduled Jobs**
```bash
# Check for running jobs in logs
docker logs espn-service --tail 20 | grep -i "job"

# Jobs will complete their current execution before stopping
```

**4. Shutdown Service**
```bash
# Docker deployment
docker-compose down espn-api-service

# Or direct Docker stop
docker stop espn-service

# Local development
# Press Ctrl+C in terminal running the service
```

**5. Verify Clean Shutdown**
```bash
# Check final logs
docker logs espn-service --tail 10

# Expected final entries:
# "Scheduler QuartzScheduler Shutdown complete"
# "Application is shutting down..."
```

---

## Health Monitoring Procedures

### 🔍 Continuous Health Monitoring

#### Basic Health Check Script
```bash
#!/bin/bash
# save as health_monitor.sh

SERVICE_URL="http://localhost:5000"
ALERT_THRESHOLD=3  # failures before alerting

check_health() {
    response=$(curl -s -w "%{http_code}" -o /tmp/health_response.json "$SERVICE_URL/health")
    
    if [ "$response" = "200" ]; then
        status=$(cat /tmp/health_response.json | jq -r '.status')
        if [ "$status" = "Healthy" ]; then
            echo "✅ Service is healthy"
            return 0
        else
            echo "⚠️  Service is $status"
            return 1
        fi
    else
        echo "❌ Health check failed with HTTP $response"
        return 2
    fi
}

# Run health check
check_health
exit $?
```

#### Automated Health Monitoring
```bash
# Add to crontab for every minute monitoring
# crontab -e
*/1 * * * * /path/to/health_monitor.sh >> /var/log/espn-health.log 2>&1
```

### 📊 Metrics Monitoring

#### Key Metrics to Monitor

**Performance Metrics**
```bash
# API Response Time (should be < 500ms average)
curl -s http://localhost:5000/metrics | jq '.performance.apiResponseTime.average'

# Cache Hit Rate (should be > 80%)
curl -s http://localhost:5000/metrics | jq '.performance.cacheMetrics.hitRate'

# Memory Usage (monitor for leaks)
curl -s http://localhost:5000/system-info | jq '.system.workingSet'
```

**Business Metrics**
```bash
# Error Rate (should be < 1%)
curl -s http://localhost:5000/metrics | jq '.business.errorRate'

# API Calls Today
curl -s http://localhost:5000/metrics | jq '.business.apiCallsToday'

# Data Processing Volume
curl -s http://localhost:5000/metrics | jq '.business.dataVolumeGB'
```

#### Metrics Collection Script
```bash
#!/bin/bash
# save as collect_metrics.sh

TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")
METRICS_FILE="/var/log/espn-metrics.log"

echo "[$TIMESTAMP] Collecting metrics..." | tee -a $METRICS_FILE

# Collect key metrics
API_RESPONSE_TIME=$(curl -s http://localhost:5000/metrics | jq -r '.performance.apiResponseTime.average // "N/A"')
CACHE_HIT_RATE=$(curl -s http://localhost:5000/metrics | jq -r '.performance.cacheMetrics.hitRate // "N/A"')
ERROR_RATE=$(curl -s http://localhost:5000/metrics | jq -r '.business.errorRate // "N/A"')
MEMORY_MB=$(curl -s http://localhost:5000/system-info | jq -r '(.system.workingSet / 1024 / 1024 | floor) // "N/A"')

echo "[$TIMESTAMP] API_RESPONSE_TIME=$API_RESPONSE_TIME ms" | tee -a $METRICS_FILE
echo "[$TIMESTAMP] CACHE_HIT_RATE=$CACHE_HIT_RATE %" | tee -a $METRICS_FILE
echo "[$TIMESTAMP] ERROR_RATE=$ERROR_RATE %" | tee -a $METRICS_FILE
echo "[$TIMESTAMP] MEMORY_USAGE=$MEMORY_MB MB" | tee -a $METRICS_FILE
```

---

## Alert Investigation and Response

### 🚨 Alert Response Procedures

#### Alert Severity Levels and Response Times

| Severity | Response Time | Escalation |
|----------|---------------|------------|
| Critical | 15 minutes | Immediate on-call |
| High | 1 hour | Within business hours |
| Medium | 4 hours | Next business day |
| Low | 24 hours | Weekly review |

#### Alert Investigation Workflow

**1. Identify Active Alerts**
```bash
# Get all active alerts
curl -s http://localhost:5000/alerts | jq '.alerts[] | select(.status == "Active")'

# Check alert summary
curl -s http://localhost:5000/alerts | jq '.summary'
```

**2. Gather Context Information**
```bash
# Get full diagnostic report
curl -s http://localhost:5000/full-diagnostic > alert_investigation_$(date +%Y%m%d_%H%M%S).json

# Check recent logs
docker logs espn-service --tail 100 > recent_logs_$(date +%Y%m%d_%H%M%S).log
```

**3. Analyze Alert Details**
```bash
# For each active alert, examine:
# - Current value vs threshold
# - Time triggered
# - Component affected
# - Historical trend

curl -s http://localhost:5000/alerts | jq '.alerts[] | {
  type: .type,
  component: .component,
  severity: .severity,
  currentValue: .currentValue,
  threshold: .threshold,
  triggeredAt: .triggeredAt
}'
```

### 🔧 Common Alert Scenarios

#### High API Response Time Alert
```bash
# 1. Check current API performance
curl -s http://localhost:5000/metrics | jq '.performance.apiResponseTime'

# 2. Verify ESPN API status
curl -w "Response Time: %{time_total}s\n" -s "https://sports.core.api.espn.com/v3/sports/football/nfl/scoreboard" > /dev/null

# 3. Check for rate limiting
docker logs espn-service --tail 50 | grep -i "rate\|limit\|429"

# 4. Review connection pool
curl -s http://localhost:5000/system-info | jq '.system'

# Resolution actions:
# - If ESPN API is slow: Wait for improvement, consider caching
# - If rate limited: Reduce request frequency
# - If connection issues: Check network connectivity
```

#### Low Cache Hit Rate Alert
```bash
# 1. Analyze cache performance
curl -s http://localhost:5000/metrics | jq '.performance.cacheMetrics'

# 2. Check cache configuration
curl -s http://localhost:5000/config | jq '.configuration.cache'

# 3. Review cache patterns in logs
docker logs espn-service --tail 100 | grep -i "cache"

# Resolution actions:
# - Increase TTL for stable data
# - Implement cache warming
# - Review cache key patterns
```

#### Memory Usage Alert
```bash
# 1. Check current memory usage
curl -s http://localhost:5000/system-info | jq '.system | {workingSet, privateMemory, peakWorkingSet}'

# 2. Analyze memory trends
curl -s http://localhost:5000/metrics | jq '.performance.memoryUsage'

# 3. Check for memory leaks
docker logs espn-service --tail 100 | grep -i "gc\|memory\|outofmemory"

# Resolution actions:
# - Restart service if memory leak suspected
# - Reduce cache size
# - Review bulk operation batch sizes
```

---

## Performance Troubleshooting

### 📈 Performance Analysis Procedures

#### Performance Baseline Verification
```bash
# Check current performance against baselines
echo "=== Performance Baseline Check ==="

# API Response Time (baseline: < 500ms)
API_TIME=$(curl -s http://localhost:5000/metrics | jq -r '.performance.apiResponseTime.average')
echo "API Response Time: $API_TIME ms (baseline: < 500ms)"

# Cache Hit Rate (baseline: > 80%)
CACHE_RATE=$(curl -s http://localhost:5000/metrics | jq -r '.performance.cacheMetrics.hitRate')
echo "Cache Hit Rate: $CACHE_RATE% (baseline: > 80%)"

# Memory Usage (baseline: < 512MB)
MEMORY_MB=$(curl -s http://localhost:5000/system-info | jq -r '(.system.workingSet / 1024 / 1024 | floor)')
echo "Memory Usage: $MEMORY_MB MB (baseline: < 512MB)"

# Error Rate (baseline: < 1%)
ERROR_RATE=$(curl -s http://localhost:5000/metrics | jq -r '.business.errorRate')
echo "Error Rate: $ERROR_RATE% (baseline: < 1%)"
```

#### Performance Deep Dive
```bash
# 1. Analyze response time distribution
curl -s http://localhost:5000/metrics | jq '.performance.apiResponseTime.percentiles'

# 2. Check for performance anomalies
curl -s http://localhost:5000/full-diagnostic | jq '.recommendations'

# 3. Review recent performance trends
docker logs espn-service --tail 200 | grep -i "performance\|slow\|timeout"
```

### 🔧 Performance Optimization Actions

#### Cache Optimization
```bash
# Review cache configuration
curl -s http://localhost:5000/config | jq '.configuration.cache'

# Suggested optimizations:
# 1. Increase TTL for stable data (season data, team info)
# 2. Implement cache warming for frequently accessed data
# 3. Add cache layers for different data types
```

#### Memory Optimization
```bash
# Monitor GC patterns
curl -s http://localhost:5000/system-info | jq '.runtime'

# Check for memory pressure
docker stats espn-service --no-stream

# Optimization actions:
# - Reduce batch sizes for bulk operations
# - Implement memory-efficient data processing
# - Tune GC settings if needed
```

---

## Maintenance Procedures

### 🔧 Routine Maintenance

#### Weekly Maintenance Checklist
- [ ] Review performance metrics trends
- [ ] Analyze error logs for patterns
- [ ] Verify backup procedures
- [ ] Check disk space usage
- [ ] Review alert history
- [ ] Update dependency vulnerabilities
- [ ] Test disaster recovery procedures

#### Monthly Maintenance Checklist
- [ ] Review and tune performance thresholds
- [ ] Analyze capacity requirements
- [ ] Update documentation
- [ ] Security review and updates
- [ ] Configuration review and optimization
- [ ] Load testing validation

### 🔄 Configuration Updates

#### Safe Configuration Change Procedure
**1. Backup Current Configuration**
```bash
# Backup current configuration
kubectl get configmap espn-config -o yaml > backup_config_$(date +%Y%m%d).yaml

# Or for Docker
docker exec espn-service cat /app/appsettings.json > backup_appsettings_$(date +%Y%m%d).json
```

**2. Validate New Configuration**
```bash
# Test configuration syntax
jq . new_appsettings.json

# Validate configuration values
# - Check numeric ranges
# - Verify URL formats
# - Validate time intervals
```

**3. Apply Configuration Changes**
```bash
# Update configuration
docker cp new_appsettings.json espn-service:/app/appsettings.json

# Restart service
docker restart espn-service
```

**4. Verify Changes**
```bash
# Wait for service startup
sleep 30

# Verify configuration applied
curl -s http://localhost:5000/config | jq '.configuration'

# Test service functionality
curl -f http://localhost:5000/health
```

### 📊 Log Management

#### Log Rotation and Cleanup
```bash
#!/bin/bash
# save as log_cleanup.sh

LOG_DIR="/app/logs"
RETENTION_DAYS=30

# Rotate logs older than 30 days
find $LOG_DIR -name "*.txt" -mtime +$RETENTION_DAYS -delete
find $LOG_DIR -name "*.json" -mtime +$RETENTION_DAYS -delete

# Compress logs older than 7 days
find $LOG_DIR -name "*.txt" -mtime +7 -exec gzip {} \;

echo "Log cleanup completed: $(date)"
```

#### Log Analysis Commands
```bash
# Error pattern analysis
grep '"level":"Error"' logs/espn-scrape-*.json | jq -r '.message' | sort | uniq -c | sort -nr

# Performance analysis
grep '"duration"' logs/espn-scrape-*.json | jq -r '.duration' | awk '{sum+=$1; count++} END {print "Average:", sum/count "ms"}'

# Correlation ID tracking
grep "correlation-id-123" logs/espn-scrape-*.json | jq -r '.message'
```

---

## Incident Response

### 🚨 Incident Response Procedures

#### Incident Severity Classification

| Severity | Definition | Response |
|----------|------------|----------|
| P1 - Critical | Complete service outage | Immediate response, all hands |
| P2 - High | Significant degradation | 1-hour response time |
| P3 - Medium | Minor impact, workaround available | 4-hour response time |
| P4 - Low | Minimal impact | Next business day |

#### Incident Response Workflow

**1. Incident Detection**
```bash
# Automated detection triggers:
# - Health check failures
# - Critical alerts
# - External monitoring systems
# - User reports

# Immediate assessment
curl -s http://localhost:5000/full-diagnostic > incident_diagnosis_$(date +%Y%m%d_%H%M%S).json
```

**2. Initial Response (< 5 minutes)**
```bash
# Quick status assessment
echo "=== Incident Response - Initial Assessment ==="
echo "Time: $(date)"
echo "Incident ID: INC-$(date +%Y%m%d-%H%M%S)"

# Check service status
SERVICE_STATUS=$(curl -s -w "%{http_code}" -o /dev/null http://localhost:5000/health)
echo "Service HTTP Status: $SERVICE_STATUS"

# Check recent alerts
CRITICAL_ALERTS=$(curl -s http://localhost:5000/alerts | jq -r '.summary.critical')
echo "Critical Alerts: $CRITICAL_ALERTS"

# Check system resources
MEMORY_MB=$(curl -s http://localhost:5000/system-info | jq -r '(.system.workingSet / 1024 / 1024 | floor)')
echo "Memory Usage: $MEMORY_MB MB"
```

**3. Detailed Investigation**
```bash
# Collect diagnostic information
mkdir -p incident_data/$(date +%Y%m%d_%H%M%S)
cd incident_data/$(date +%Y%m%d_%H%M%S)

# System state
curl -s http://localhost:5000/full-diagnostic > full_diagnostic.json
curl -s http://localhost:5000/alerts > alerts.json
curl -s http://localhost:5000/metrics > metrics.json

# Recent logs
docker logs espn-service --tail 500 > recent_logs.txt

# System resources
docker stats espn-service --no-stream > system_stats.txt
```

**4. Resolution Actions**

**Service Restart (if needed)**
```bash
# Graceful restart
docker restart espn-service

# Wait for service recovery
sleep 60

# Verify restoration
curl -f http://localhost:5000/health
```

**Configuration Rollback (if needed)**
```bash
# Restore previous configuration
docker cp backup_appsettings_YYYYMMDD.json espn-service:/app/appsettings.json
docker restart espn-service
```

#### Post-Incident Review

**1. Service Validation**
```bash
# Comprehensive service validation
./health_monitor.sh
./collect_metrics.sh

# Run for 15 minutes to ensure stability
for i in {1..15}; do
    sleep 60
    curl -f http://localhost:5000/health || echo "Health check failed at minute $i"
done
```

**2. Incident Documentation**
```bash
# Create incident report
cat > incident_report_$(date +%Y%m%d).md << EOF
# Incident Report - $(date +%Y%m%d)

## Incident Summary
- **Incident ID**: INC-$(date +%Y%m%d-%H%M%S)
- **Severity**: [P1/P2/P3/P4]
- **Start Time**: $(date)
- **Duration**: [Duration]
- **Impact**: [Description]

## Root Cause
[Root cause analysis]

## Resolution
[Actions taken to resolve]

## Prevention
[Actions to prevent recurrence]

## Lessons Learned
[Key takeaways]
EOF
```

---

## Backup and Recovery

### 💾 Backup Procedures

#### Configuration Backup
```bash
#!/bin/bash
# save as backup_config.sh

BACKUP_DIR="/backups/espn-service"
DATE=$(date +%Y%m%d_%H%M%S)

mkdir -p $BACKUP_DIR

# Backup configuration files
docker cp espn-service:/app/appsettings.json $BACKUP_DIR/appsettings_$DATE.json
docker cp espn-service:/app/appsettings.Production.json $BACKUP_DIR/appsettings.Production_$DATE.json

# Backup Docker Compose files
cp docker-compose.yml $BACKUP_DIR/docker-compose_$DATE.yml

echo "Configuration backup completed: $BACKUP_DIR"
```

#### Data Backup
```bash
#!/bin/bash
# save as backup_data.sh

BACKUP_DIR="/backups/espn-service"
DATE=$(date +%Y%m%d_%H%M%S)

# Backup logs
tar -czf $BACKUP_DIR/logs_$DATE.tar.gz logs/

# Backup downloaded data
tar -czf $BACKUP_DIR/downloads_$DATE.tar.gz downloads/

# Backup metrics data (if persisted)
docker exec espn-service find /app -name "*.db" -exec cp {} /tmp/backup/ \;

echo "Data backup completed: $BACKUP_DIR"
```

### 🔄 Recovery Procedures

#### Configuration Recovery
```bash
# Restore configuration from backup
BACKUP_FILE="/backups/espn-service/appsettings_YYYYMMDD_HHMMSS.json"

# Validate backup file
jq . $BACKUP_FILE

# Restore configuration
docker cp $BACKUP_FILE espn-service:/app/appsettings.json
docker restart espn-service

# Verify recovery
curl -f http://localhost:5000/health
```

#### Complete Service Recovery
```bash
#!/bin/bash
# save as recover_service.sh

echo "Starting service recovery..."

# 1. Stop current service
docker-compose down espn-api-service

# 2. Restore configuration
LATEST_CONFIG=$(ls -t /backups/espn-service/appsettings_*.json | head -1)
cp $LATEST_CONFIG ./appsettings.json

# 3. Restore Docker Compose
LATEST_COMPOSE=$(ls -t /backups/espn-service/docker-compose_*.yml | head -1)
cp $LATEST_COMPOSE ./docker-compose.yml

# 4. Start service
docker-compose up -d espn-api-service

# 5. Wait for startup
sleep 60

# 6. Verify recovery
if curl -f http://localhost:5000/health; then
    echo "✅ Service recovery successful"
else
    echo "❌ Service recovery failed"
    exit 1
fi
```

---

## Emergency Contacts and Escalation

### 📞 Emergency Response Team

| Role | Primary Contact | Secondary Contact |
|------|----------------|-------------------|
| On-Call Engineer | [Phone/Email] | [Phone/Email] |
| Service Owner | [Phone/Email] | [Phone/Email] |
| Platform Team | [Phone/Email] | [Phone/Email] |
| Management | [Phone/Email] | [Phone/Email] |

### 📋 Escalation Procedures

**Immediate Escalation (P1 Incidents)**
1. Page on-call engineer immediately
2. Create incident channel in Slack/Teams
3. Notify service owner within 15 minutes
4. Engage platform team if infrastructure related

**Standard Escalation (P2/P3 Incidents)**
1. Email on-call engineer
2. Create incident ticket
3. Follow up if no response within SLA

---

For additional procedures and troubleshooting, see:
- [Troubleshooting Guide](TROUBLESHOOTING.md)
- [Performance Tuning Guide](PERFORMANCE_TUNING.md)
- [API Documentation](API_DOCUMENTATION.md)