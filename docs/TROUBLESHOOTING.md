# ESPN API Service - Troubleshooting Guide

This comprehensive guide provides diagnostic procedures and solutions for common issues encountered with the ESPN API service.

## Table of Contents
- [Quick Diagnostic Commands](#quick-diagnostic-commands)
- [Common Issues and Solutions](#common-issues-and-solutions)
- [Error Code Reference](#error-code-reference)
- [Performance Issues](#performance-issues)
- [Connectivity Problems](#connectivity-problems)
- [Cache-Related Issues](#cache-related-issues)
- [Scheduled Job Problems](#scheduled-job-problems)
- [Memory and Resource Issues](#memory-and-resource-issues)
- [Diagnostic Tools and Scripts](#diagnostic-tools-and-scripts)

---

## Quick Diagnostic Commands

### 🔍 Initial Health Assessment
```bash
#!/bin/bash
# Quick health check script

echo "=== ESPN API Service Health Check ==="
echo "Timestamp: $(date)"

# 1. Service availability
echo "1. Service Health:"
SERVICE_STATUS=$(curl -s -w "%{http_code}" -o /tmp/health.json http://localhost:5000/health)
if [ "$SERVICE_STATUS" = "200" ]; then
    echo "✅ Service is responding"
    HEALTH_STATUS=$(cat /tmp/health.json | jq -r '.status // "Unknown"')
    echo "   Health Status: $HEALTH_STATUS"
else
    echo "❌ Service not responding (HTTP $SERVICE_STATUS)"
fi

# 2. Critical metrics
echo "2. Key Metrics:"
if [ "$SERVICE_STATUS" = "200" ]; then
    curl -s http://localhost:5000/metrics | jq -r '
        "   API Response Time: " + (.performance.apiResponseTime.average // "N/A" | tostring) + "ms",
        "   Cache Hit Rate: " + (.performance.cacheMetrics.hitRate // "N/A" | tostring) + "%",
        "   Error Rate: " + (.business.errorRate // "N/A" | tostring) + "%",
        "   API Calls Today: " + (.business.apiCallsToday // "N/A" | tostring)
    '
fi

# 3. Active alerts
echo "3. Active Alerts:"
ALERT_COUNT=$(curl -s http://localhost:5000/alerts | jq -r '.summary.total // 0')
echo "   Total Alerts: $ALERT_COUNT"

# 4. System resources
echo "4. System Resources:"
MEMORY_MB=$(curl -s http://localhost:5000/system-info | jq -r '(.system.workingSet // 0) / 1024 / 1024 | floor')
echo "   Memory Usage: ${MEMORY_MB}MB"

echo "=== Health Check Complete ==="
```

### 🚀 Service Status Verification
```bash
# Check if service is running
docker ps | grep espn-service

# Check service logs for errors
docker logs espn-service --tail 20 | grep -i "error\|exception\|fail"

# Verify all endpoints respond
for endpoint in health metrics alerts system-info config; do
    status=$(curl -s -w "%{http_code}" -o /dev/null http://localhost:5000/$endpoint)
    echo "$endpoint: HTTP $status"
done
```

---

## Common Issues and Solutions

### 🔧 Service Won't Start

#### Symptoms
- Container exits immediately
- Health check endpoints not responding
- "Application failed to start" in logs

#### Diagnostic Steps
```bash
# 1. Check container status
docker ps -a | grep espn-service

# 2. Examine startup logs
docker logs espn-service

# 3. Check port conflicts
netstat -tulpn | grep :5000

# 4. Verify configuration files
docker exec espn-service cat /app/appsettings.json | jq .
```

#### Common Causes and Solutions

**Configuration Issues**
```bash
# Check for JSON syntax errors
docker exec espn-service cat /app/appsettings.json | jq . > /dev/null
if [ $? -ne 0 ]; then
    echo "Configuration file has JSON syntax errors"
fi

# Validate required configuration sections
docker exec espn-service cat /app/appsettings.json | jq -e '.Serilog, .Quartz, .Cache' > /dev/null
if [ $? -ne 0 ]; then
    echo "Missing required configuration sections"
fi
```

**Port Already in Use**
```bash
# Find process using port 5000
lsof -i :5000

# Kill conflicting process or change port
docker run -p 5001:80 espn-api-service
```

**Missing Dependencies**
```bash
# Check for missing files
docker exec espn-service ls -la /app/

# Verify .NET runtime
docker exec espn-service dotnet --version
```

**File Permissions**
```bash
# Check log directory permissions
docker exec espn-service ls -la /app/logs/

# Fix permissions if needed
docker exec espn-service chmod -R 755 /app/logs/
```

### 🌐 ESPN API Connectivity Issues

#### Symptoms
- "ESPN API is not accessible" health check failures
- Timeouts in service logs
- Empty or null API responses

#### Diagnostic Steps
```bash
# 1. Test ESPN API directly
curl -w "Time: %{time_total}s\nStatus: %{http_code}\n" \
     "https://sports.core.api.espn.com/v3/sports/football/nfl/scoreboard"

# 2. Check DNS resolution
nslookup sports.core.api.espn.com

# 3. Test network connectivity
ping sports.core.api.espn.com

# 4. Check for proxy/firewall issues
curl -v "https://sports.core.api.espn.com/v3/sports/football/nfl/scoreboard" 2>&1 | grep -i "proxy\|tunnel"
```

#### Solutions

**Network Connectivity**
```bash
# Check network configuration
docker exec espn-service cat /etc/resolv.conf

# Test different DNS servers
docker exec espn-service nslookup sports.core.api.espn.com 8.8.8.8
```

**Rate Limiting**
```bash
# Check for rate limit errors
docker logs espn-service | grep -i "429\|rate.*limit"

# Verify rate limiting configuration
curl -s http://localhost:5000/config | jq '.configuration.rateLimiting'
```

**SSL/TLS Issues**
```bash
# Test SSL connection
echo | openssl s_client -connect sports.core.api.espn.com:443 -servername sports.core.api.espn.com

# Check certificate validity
curl -vI "https://sports.core.api.espn.com/v3/sports/football/nfl/scoreboard" 2>&1 | grep -i certificate
```

### 📊 High Memory Usage

#### Symptoms
- Memory usage > 500MB consistently
- OutOfMemoryException in logs
- Slow response times

#### Diagnostic Steps
```bash
# 1. Check current memory usage
docker stats espn-service --no-stream

# 2. Analyze memory breakdown
curl -s http://localhost:5000/system-info | jq '.system | {
    workingSet: (.workingSet / 1024 / 1024 | floor),
    privateMemory: (.privateMemory / 1024 / 1024 | floor),
    peakWorkingSet: (.peakWorkingSet / 1024 / 1024 | floor)
}'

# 3. Check for memory leaks
docker logs espn-service | grep -i "gc\|garbage\|memory"
```

#### Solutions

**Cache Optimization**
```bash
# Reduce cache size
curl -X POST http://localhost:5000/config \
     -H "Content-Type: application/json" \
     -d '{"cache": {"memoryLimitMB": 128}}'

# Clear cache if needed
curl -X POST http://localhost:5000/cache/clear
```

**Batch Size Reduction**
```bash
# Check current batch sizes
curl -s http://localhost:5000/config | jq '.configuration.processing.batchSize'

# Reduce batch sizes for bulk operations
# Update configuration to process smaller chunks
```

### ⚡ Slow Performance

#### Symptoms
- API response times > 1000ms
- Cache hit rate < 50%
- Timeouts on complex requests

#### Diagnostic Steps
```bash
# 1. Analyze performance metrics
curl -s http://localhost:5000/metrics | jq '.performance'

# 2. Check response time distribution
curl -s http://localhost:5000/metrics | jq '.performance.apiResponseTime.percentiles'

# 3. Identify slow endpoints
docker logs espn-service --tail 100 | grep -E "duration.*[0-9]{4,}" | sort -k4 -nr | head -10
```

#### Solutions

**Cache Optimization**
```bash
# Increase cache TTL for stable data
curl -s http://localhost:5000/config | jq '.configuration.cache.defaultTtlMinutes'

# Implement cache warming
curl -X POST http://localhost:5000/cache/warm
```

**Request Optimization**
```bash
# Reduce concurrent request limits
curl -s http://localhost:5000/config | jq '.configuration.rateLimiting.maxConcurrentRequests'

# Enable request compression
curl -s http://localhost:5000/config | jq '.configuration.http.enableCompression'
```

---

## Error Code Reference

### 🚨 HTTP Error Codes

| Code | Meaning | Likely Cause | Action |
|------|---------|--------------|---------|
| 400 | Bad Request | Invalid parameters or malformed request | Check request format and parameters |
| 401 | Unauthorized | Missing or invalid authentication | Verify authentication credentials |
| 403 | Forbidden | Insufficient permissions | Check API key permissions |
| 404 | Not Found | Resource doesn't exist | Verify resource ID or endpoint |
| 429 | Too Many Requests | Rate limit exceeded | Implement backoff, reduce request rate |
| 500 | Internal Server Error | Server-side error | Check logs, retry request |
| 502 | Bad Gateway | Upstream server error | Wait and retry, check connectivity |
| 503 | Service Unavailable | Server temporarily unavailable | Implement retry with backoff |
| 504 | Gateway Timeout | Request timeout | Increase timeout, optimize request |

### 🔍 Application Error Codes

| Error Code | Description | Resolution |
|------------|-------------|------------|
| ESPN_001 | ESPN API connection failed | Check network connectivity |
| ESPN_002 | Rate limit exceeded | Reduce request frequency |
| ESPN_003 | Invalid response format | Check ESPN API changes |
| ESPN_004 | Cache operation failed | Check cache service status |
| ESPN_005 | Configuration error | Validate configuration file |
| ESPN_006 | Job execution failed | Check scheduler and job status |
| ESPN_007 | Data validation failed | Review data quality rules |
| ESPN_008 | Resource exhaustion | Check memory and CPU usage |

### 📋 Error Investigation Script
```bash
#!/bin/bash
# Error investigation script

ERROR_CODE="$1"
if [ -z "$ERROR_CODE" ]; then
    echo "Usage: $0 <error_code>"
    exit 1
fi

echo "=== Investigating Error Code: $ERROR_CODE ==="

case "$ERROR_CODE" in
    "ESPN_001")
        echo "ESPN API Connection Failed"
        echo "Checking connectivity..."
        curl -w "Time: %{time_total}s\nStatus: %{http_code}\n" \
             "https://sports.core.api.espn.com/v3/sports/football/nfl/scoreboard"
        ;;
    "ESPN_002")
        echo "Rate Limit Exceeded"
        echo "Checking rate limiting status..."
        curl -s http://localhost:5000/metrics | jq '.business.apiCallsToday'
        docker logs espn-service --tail 50 | grep -i "rate\|429"
        ;;
    "ESPN_003")
        echo "Invalid Response Format"
        echo "Checking recent API responses..."
        docker logs espn-service --tail 100 | grep -i "json\|parse\|format"
        ;;
    "ESPN_004")
        echo "Cache Operation Failed"
        echo "Checking cache status..."
        curl -s http://localhost:5000/metrics | jq '.performance.cacheMetrics'
        ;;
    *)
        echo "Unknown error code. Checking general logs..."
        docker logs espn-service --tail 20 | grep -i "$ERROR_CODE"
        ;;
esac
```

---

## Performance Issues

### 📈 Performance Monitoring

#### Real-time Performance Check
```bash
#!/bin/bash
# Performance monitoring script

echo "=== Performance Monitoring ==="

# 1. Response time analysis
echo "1. API Response Times:"
curl -s http://localhost:5000/metrics | jq -r '
    .performance.apiResponseTime | 
    "   Average: " + (.average // "N/A" | tostring) + "ms",
    "   P50: " + (.percentiles.p50 // "N/A" | tostring) + "ms",
    "   P95: " + (.percentiles.p95 // "N/A" | tostring) + "ms",
    "   P99: " + (.percentiles.p99 // "N/A" | tostring) + "ms"
'

# 2. Cache performance
echo "2. Cache Performance:"
curl -s http://localhost:5000/metrics | jq -r '
    .performance.cacheMetrics |
    "   Hit Rate: " + (.hitRate // "N/A" | tostring) + "%",
    "   Total Requests: " + (.totalRequests // "N/A" | tostring),
    "   Cache Hits: " + (.cacheHits // "N/A" | tostring)
'

# 3. System resources
echo "3. System Resources:"
curl -s http://localhost:5000/system-info | jq -r '
    .system |
    "   Memory: " + ((.workingSet // 0) / 1024 / 1024 | floor | tostring) + "MB",
    "   CPU Usage: " + (.cpuUsage // "N/A" | tostring) + "%",
    "   GC Collections: " + (.runtime.gcCollections // "N/A" | tostring)
'
```

#### Performance Trend Analysis
```bash
#!/bin/bash
# Collect performance data over time

DURATION_MINUTES=${1:-10}
INTERVAL_SECONDS=${2:-30}

echo "Collecting performance data for $DURATION_MINUTES minutes..."

for i in $(seq 1 $((DURATION_MINUTES * 60 / INTERVAL_SECONDS))); do
    TIMESTAMP=$(date "+%Y-%m-%d %H:%M:%S")
    API_TIME=$(curl -s http://localhost:5000/metrics | jq -r '.performance.apiResponseTime.average // "N/A"')
    CACHE_RATE=$(curl -s http://localhost:5000/metrics | jq -r '.performance.cacheMetrics.hitRate // "N/A"')
    MEMORY_MB=$(curl -s http://localhost:5000/system-info | jq -r '(.system.workingSet // 0) / 1024 / 1024 | floor')
    
    echo "$TIMESTAMP,$API_TIME,$CACHE_RATE,$MEMORY_MB"
    sleep $INTERVAL_SECONDS
done
```

### 🎯 Performance Optimization

#### Cache Warming Script
```bash
#!/bin/bash
# Cache warming script

echo "Starting cache warming..."

# Warm current week data
CURRENT_WEEK=$(date +%V)
curl -s "http://localhost:5000/api/scoreboard?week=$CURRENT_WEEK" > /dev/null

# Warm popular teams
POPULAR_TEAMS=(1 2 3 4 5 6 7 8 9 10)
for team_id in "${POPULAR_TEAMS[@]}"; do
    echo "Warming team $team_id..."
    curl -s "http://localhost:5000/api/team/$team_id" > /dev/null
    sleep 1
done

echo "Cache warming completed"
```

#### Memory Optimization
```bash
#!/bin/bash
# Memory optimization script

echo "Optimizing memory usage..."

# Force garbage collection
curl -X POST http://localhost:5000/system/gc

# Clear old cache entries
curl -X POST http://localhost:5000/cache/cleanup

# Restart if memory usage too high
MEMORY_MB=$(curl -s http://localhost:5000/system-info | jq -r '(.system.workingSet // 0) / 1024 / 1024 | floor')
if [ "$MEMORY_MB" -gt 800 ]; then
    echo "Memory usage too high ($MEMORY_MB MB), considering restart"
    # Implement restart logic if needed
fi
```

---

## Connectivity Problems

### 🌐 Network Diagnostics

#### Network Connectivity Test
```bash
#!/bin/bash
# Comprehensive network test

echo "=== Network Connectivity Test ==="

# 1. DNS Resolution
echo "1. DNS Resolution:"
for host in sports.core.api.espn.com site.api.espn.com; do
    if nslookup $host > /dev/null 2>&1; then
        echo "   ✅ $host resolves"
    else
        echo "   ❌ $host fails to resolve"
    fi
done

# 2. Ping test
echo "2. Ping Test:"
if ping -c 3 sports.core.api.espn.com > /dev/null 2>&1; then
    echo "   ✅ ESPN servers reachable"
else
    echo "   ❌ ESPN servers unreachable"
fi

# 3. HTTP connectivity
echo "3. HTTP Connectivity:"
HTTP_STATUS=$(curl -s -w "%{http_code}" -o /dev/null --max-time 10 \
              "https://sports.core.api.espn.com/v3/sports/football/nfl/scoreboard")
if [ "$HTTP_STATUS" = "200" ]; then
    echo "   ✅ ESPN API accessible (HTTP $HTTP_STATUS)"
else
    echo "   ❌ ESPN API not accessible (HTTP $HTTP_STATUS)"
fi

# 4. SSL/TLS test
echo "4. SSL/TLS Test:"
if echo | openssl s_client -connect sports.core.api.espn.com:443 -servername sports.core.api.espn.com > /dev/null 2>&1; then
    echo "   ✅ SSL connection successful"
else
    echo "   ❌ SSL connection failed"
fi
```

#### Proxy Configuration
```bash
# Check for proxy settings
env | grep -i proxy

# Test with proxy bypass
curl --noproxy "*" "https://sports.core.api.espn.com/v3/sports/football/nfl/scoreboard"

# Configure proxy if needed
export http_proxy=http://proxy.company.com:8080
export https_proxy=http://proxy.company.com:8080
```

### 🔧 Connection Pool Issues

#### Connection Pool Diagnostics
```bash
# Check active connections
curl -s http://localhost:5000/system-info | jq '.system.connectionPool'

# Monitor connection metrics
docker logs espn-service --tail 100 | grep -i "connection\|pool\|timeout"
```

#### Connection Pool Optimization
```bash
# Reset connection pool
curl -X POST http://localhost:5000/system/reset-connections

# Adjust pool settings (via configuration update)
curl -X PATCH http://localhost:5000/config \
     -H "Content-Type: application/json" \
     -d '{"http": {"maxConnectionsPerServer": 10, "connectionTimeout": 30}}'
```

---

## Cache-Related Issues

### 💾 Cache Diagnostics

#### Cache Health Check
```bash
#!/bin/bash
# Cache health diagnostics

echo "=== Cache Health Check ==="

# 1. Cache metrics
echo "1. Cache Metrics:"
curl -s http://localhost:5000/metrics | jq -r '
    .performance.cacheMetrics |
    "   Hit Rate: " + (.hitRate // "N/A" | tostring) + "%",
    "   Total Requests: " + (.totalRequests // "N/A" | tostring),
    "   Cache Size: " + (.cacheSizeMB // "N/A" | tostring) + "MB"
'

# 2. Cache configuration
echo "2. Cache Configuration:"
curl -s http://localhost:5000/config | jq '.configuration.cache'

# 3. Recent cache activity
echo "3. Recent Cache Activity:"
docker logs espn-service --tail 50 | grep -i "cache" | tail -10
```

#### Cache Performance Issues

**Low Hit Rate**
```bash
# Analyze cache patterns
docker logs espn-service --tail 200 | grep -i "cache.*miss" | 
  sed -E 's/.*cache.*miss.*key:([^,]+).*/\1/' | sort | uniq -c | sort -nr

# Check TTL settings
curl -s http://localhost:5000/config | jq '.configuration.cache.defaultTtlMinutes'
```

**Cache Memory Issues**
```bash
# Check cache memory usage
curl -s http://localhost:5000/metrics | jq '.performance.cacheMetrics.cacheSizeMB'

# Clear cache if needed
curl -X POST http://localhost:5000/cache/clear

# Reduce cache size
curl -X PATCH http://localhost:5000/config \
     -H "Content-Type: application/json" \
     -d '{"cache": {"memoryLimitMB": 128}}'
```

---

## Scheduled Job Problems

### ⏰ Job Monitoring

#### Job Status Check
```bash
#!/bin/bash
# Job status monitoring

echo "=== Scheduled Job Status ==="

# Check Quartz scheduler status
docker logs espn-service | grep -i "quartz\|scheduler" | tail -10

# Look for job execution logs
docker logs espn-service --tail 100 | grep -i "job.*executing\|job.*completed"

# Check for job failures
docker logs espn-service | grep -i "job.*error\|job.*failed\|job.*exception"
```

#### Job Troubleshooting

**Jobs Not Running**
```bash
# Check scheduler initialization
docker logs espn-service | grep -i "scheduler.*start"

# Verify job registration
docker logs espn-service | grep -i "adding.*job"

# Check trigger configuration
docker logs espn-service | grep -i "trigger"
```

**Job Failures**
```bash
# Analyze job failure patterns
docker logs espn-service | grep -i "job.*failed" | 
  sed -E 's/.*job.*([A-Za-z]+Job).*failed.*/\1/' | sort | uniq -c

# Check for resource constraints during job execution
docker logs espn-service | grep -E "job.*(memory|timeout|exception)"
```

### 🔧 Job Recovery

#### Manual Job Execution
```bash
# Trigger job manually (if endpoint available)
curl -X POST http://localhost:5000/jobs/execute/EspnApiScrapingJob

# Check job execution status
curl -s http://localhost:5000/jobs/status
```

#### Job Configuration Reset
```bash
# Restart scheduler
curl -X POST http://localhost:5000/scheduler/restart

# Reload job configuration
curl -X POST http://localhost:5000/scheduler/reload
```

---

## Memory and Resource Issues

### 💻 Resource Monitoring

#### System Resource Check
```bash
#!/bin/bash
# Comprehensive resource monitoring

echo "=== System Resource Check ==="

# 1. Memory usage
echo "1. Memory Usage:"
curl -s http://localhost:5000/system-info | jq -r '
    .system |
    "   Working Set: " + ((.workingSet // 0) / 1024 / 1024 | floor | tostring) + "MB",
    "   Private Memory: " + ((.privateMemory // 0) / 1024 / 1024 | floor | tostring) + "MB",
    "   Peak Memory: " + ((.peakWorkingSet // 0) / 1024 / 1024 | floor | tostring) + "MB"
'

# 2. CPU usage
echo "2. CPU Usage:"
docker stats espn-service --no-stream | awk 'NR==2 {print "   CPU: " $3}'

# 3. Disk usage
echo "3. Disk Usage:"
docker exec espn-service df -h /app

# 4. GC information
echo "4. Garbage Collection:"
curl -s http://localhost:5000/system-info | jq -r '
    .runtime |
    "   GC Collections: " + (.gcCollections // "N/A" | tostring),
    "   Gen 0 Collections: " + (.gen0Collections // "N/A" | tostring),
    "   Gen 1 Collections: " + (.gen1Collections // "N/A" | tostring),
    "   Gen 2 Collections: " + (.gen2Collections // "N/A" | tostring)
'
```

### 🔧 Resource Optimization

#### Memory Cleanup
```bash
#!/bin/bash
# Memory cleanup script

echo "Starting memory cleanup..."

# Force garbage collection
curl -X POST http://localhost:5000/system/gc
echo "✅ Forced garbage collection"

# Clear caches
curl -X POST http://localhost:5000/cache/clear
echo "✅ Cleared cache"

# Wait and check memory usage
sleep 10
MEMORY_AFTER=$(curl -s http://localhost:5000/system-info | jq -r '(.system.workingSet // 0) / 1024 / 1024 | floor')
echo "Memory usage after cleanup: ${MEMORY_AFTER}MB"
```

#### Resource Limit Configuration
```bash
# Check Docker resource limits
docker inspect espn-service | jq '.[0].HostConfig | {Memory, CpuShares, CpuQuota}'

# Update resource limits if needed
docker update --memory="512m" --cpus="1.0" espn-service
```

---

## Diagnostic Tools and Scripts

### 🛠️ Complete Diagnostic Suite

#### Full System Diagnostic
```bash
#!/bin/bash
# Complete diagnostic script

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
REPORT_DIR="diagnostic_$TIMESTAMP"
mkdir -p "$REPORT_DIR"

echo "=== ESPN API Service - Full Diagnostic Report ==="
echo "Timestamp: $(date)"
echo "Report Directory: $REPORT_DIR"

# 1. Service status
echo "Collecting service status..."
curl -s http://localhost:5000/health > "$REPORT_DIR/health.json"
curl -s http://localhost:5000/metrics > "$REPORT_DIR/metrics.json"
curl -s http://localhost:5000/system-info > "$REPORT_DIR/system-info.json"
curl -s http://localhost:5000/alerts > "$REPORT_DIR/alerts.json"
curl -s http://localhost:5000/config > "$REPORT_DIR/config.json"

# 2. Logs
echo "Collecting logs..."
docker logs espn-service --tail 500 > "$REPORT_DIR/container.log"

# 3. System information
echo "Collecting system information..."
docker stats espn-service --no-stream > "$REPORT_DIR/docker-stats.txt"
docker inspect espn-service > "$REPORT_DIR/container-inspect.json"

# 4. Network tests
echo "Running network tests..."
{
    echo "=== DNS Test ==="
    nslookup sports.core.api.espn.com
    
    echo "=== Ping Test ==="
    ping -c 3 sports.core.api.espn.com
    
    echo "=== HTTP Test ==="
    curl -w "Time: %{time_total}s\nStatus: %{http_code}\n" \
         "https://sports.core.api.espn.com/v3/sports/football/nfl/scoreboard"
} > "$REPORT_DIR/network-tests.txt" 2>&1

# 5. Generate summary
{
    echo "=== Diagnostic Summary ==="
    echo "Generated: $(date)"
    echo ""
    
    echo "Service Health:"
    if [ -f "$REPORT_DIR/health.json" ]; then
        jq -r '.status // "Unknown"' "$REPORT_DIR/health.json"
    fi
    
    echo ""
    echo "Active Alerts:"
    if [ -f "$REPORT_DIR/alerts.json" ]; then
        jq -r '.summary.total // 0' "$REPORT_DIR/alerts.json"
    fi
    
    echo ""
    echo "Key Metrics:"
    if [ -f "$REPORT_DIR/metrics.json" ]; then
        jq -r '
            "API Response Time: " + (.performance.apiResponseTime.average // "N/A" | tostring) + "ms",
            "Cache Hit Rate: " + (.performance.cacheMetrics.hitRate // "N/A" | tostring) + "%",
            "Error Rate: " + (.business.errorRate // "N/A" | tostring) + "%"
        ' "$REPORT_DIR/metrics.json"
    fi
    
    echo ""
    echo "Memory Usage:"
    if [ -f "$REPORT_DIR/system-info.json" ]; then
        jq -r '(.system.workingSet // 0) / 1024 / 1024 | floor | "Memory: " + tostring + "MB"' "$REPORT_DIR/system-info.json"
    fi
} > "$REPORT_DIR/summary.txt"

echo "✅ Diagnostic complete. Report saved to: $REPORT_DIR"
echo "📁 Files generated:"
ls -la "$REPORT_DIR/"
```

#### Log Analysis Tool
```bash
#!/bin/bash
# Log analysis tool

LOG_FILE=${1:-"$(docker logs espn-service 2>&1)"}
ANALYSIS_FILE="log_analysis_$(date +%Y%m%d_%H%M%S).txt"

echo "=== Log Analysis Report ===" > "$ANALYSIS_FILE"
echo "Generated: $(date)" >> "$ANALYSIS_FILE"
echo "" >> "$ANALYSIS_FILE"

# Error analysis
echo "=== Error Analysis ===" >> "$ANALYSIS_FILE"
echo "$LOG_FILE" | grep -i "error\|exception\|fail" | 
  sed -E 's/.*"level":"([^"]+)".*"message":"([^"]+)".*/\1: \2/' | 
  sort | uniq -c | sort -nr >> "$ANALYSIS_FILE"

echo "" >> "$ANALYSIS_FILE"

# Performance analysis
echo "=== Performance Analysis ===" >> "$ANALYSIS_FILE"
echo "$LOG_FILE" | grep -o '"duration":[0-9]*' | 
  sed 's/"duration"://' | 
  awk '{sum+=$1; count++; if($1>max) max=$1} END {
    print "Average Duration: " sum/count "ms"
    print "Max Duration: " max "ms"
    print "Total Requests: " count
  }' >> "$ANALYSIS_FILE"

echo "" >> "$ANALYSIS_FILE"

# Cache analysis
echo "=== Cache Analysis ===" >> "$ANALYSIS_FILE"
CACHE_HITS=$(echo "$LOG_FILE" | grep -c "cache.*hit")
CACHE_MISSES=$(echo "$LOG_FILE" | grep -c "cache.*miss")
if [ $((CACHE_HITS + CACHE_MISSES)) -gt 0 ]; then
    HIT_RATE=$((CACHE_HITS * 100 / (CACHE_HITS + CACHE_MISSES)))
    echo "Cache Hit Rate: $HIT_RATE%" >> "$ANALYSIS_FILE"
    echo "Cache Hits: $CACHE_HITS" >> "$ANALYSIS_FILE"
    echo "Cache Misses: $CACHE_MISSES" >> "$ANALYSIS_FILE"
fi

echo "Log analysis saved to: $ANALYSIS_FILE"
```

---

For escalation procedures and additional support:
- [Operational Runbooks](OPERATIONAL_RUNBOOKS.md)
- [Performance Tuning Guide](PERFORMANCE_TUNING.md)
- [ESPN API Best Practices](ESPN_API_BEST_PRACTICES.md)