# ESPN API Service - Architecture Documentation

This document provides comprehensive visual and textual documentation of the ESPN API service architecture, component interactions, and deployment topology.

## Table of Contents
- [System Architecture Overview](#system-architecture-overview)
- [Component Architecture](#component-architecture)
- [Data Flow Diagrams](#data-flow-diagrams)
- [Monitoring Infrastructure](#monitoring-infrastructure)
- [Deployment Architecture](#deployment-architecture)
- [Security Architecture](#security-architecture)
- [Scaling and Performance](#scaling-and-performance)

---

## System Architecture Overview

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                          ESPN API Service - System Architecture                   │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────┐    ┌──────────────────┐    ┌─────────────────────────────┐ │
│  │   Load Balancer │────│  ESPN API Service │────│     External Services       │ │
│  │                 │    │                  │    │                             │ │
│  │  • Health Check │    │  • .NET 8.0 API │    │  • ESPN Sports API          │ │
│  │  • SSL Term.    │    │  • Diagnostic    │    │  • sports.core.api.espn.com│ │
│  │  • Routing      │    │    Endpoints     │    │  • site.api.espn.com       │ │
│  └─────────────────┘    │  • Health Checks │    └─────────────────────────────┘ │
│           │              │  • Monitoring    │                    │               │
│           │              └──────────────────┘                    │               │
│           │                       │                              │               │
│           └───────────────────────┼──────────────────────────────┘               │
│                                   │                                              │
│  ┌─────────────────────────────────┼─────────────────────────────────────────────┤
│  │             Internal Architecture                                             │
│  │                                 │                                             │
│  │  ┌─────────────────┐    ┌──────┴──────┐    ┌─────────────────────────────┐   │
│  │  │     Caching     │────│   Services  │────│      Scheduled Jobs        │   │
│  │  │                 │    │             │    │                             │   │
│  │  │ • Memory Cache  │    │ • ESPN API  │    │ • Quartz.NET Scheduler      │   │
│  │  │ • Distributed   │    │ • Box Score │    │ • API Scraping Job          │   │
│  │  │   Cache (Redis) │    │ • Scoreboard│    │ • Image Scraping Job        │   │
│  │  │ • Multi-Layer   │    │ • Rate Limit│    │ • Configurable Triggers     │   │
│  │  └─────────────────┘    │ • HTTP      │    └─────────────────────────────┘   │
│  │                         │ • Cache     │                                      │
│  │  ┌─────────────────┐    └─────────────┘    ┌─────────────────────────────┐   │
│  │  │    Logging      │                       │        Monitoring           │   │
│  │  │                 │                       │                             │   │
│  │  │ • Serilog       │                       │ • Health Checks             │   │
│  │  │ • Structured    │                       │ • Metrics Collection        │   │
│  │  │ • File & Console│                       │ • Alert Processing          │   │
│  │  │ • Correlation   │                       │ • Diagnostic Endpoints      │   │
│  │  └─────────────────┘                       └─────────────────────────────┘   │
│  └─────────────────────────────────────────────────────────────────────────────┤
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Architecture Principles

1. **Microservice-Ready**: Designed as a standalone service with clear boundaries
2. **Observability-First**: Comprehensive monitoring, logging, and diagnostics
3. **Resilience**: Rate limiting, circuit breakers, retry policies
4. **Performance**: Multi-layer caching, async processing, optimized data flow
5. **Scalability**: Stateless design, horizontal scaling capability
6. **Security**: Secure by default, input validation, audit logging

---

## Component Architecture

### Core Service Components

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                            ESPN API Service - Components                         │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                              API Layer                                      │
│  │                                                                             │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │  │    Health    │  │   Metrics    │  │    Alerts    │  │    Config    │   │
│  │  │  Controller  │  │  Controller  │  │  Controller  │  │  Controller  │   │
│  │  │              │  │              │  │              │  │              │   │
│  │  │ GET /health  │  │GET /metrics  │  │GET /alerts   │  │GET /config   │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘   │
│  │                                                                             │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                     │
│  │  │ System Info  │  │Full Diagnostic│  │   Logs       │                     │
│  │  │  Controller  │  │  Controller  │  │  Controller  │                     │
│  │  │              │  │              │  │              │                     │
│  │  │GET /system   │  │GET /full-    │  │GET /logs     │                     │
│  │  │    -info     │  │   diagnostic │  │              │                     │
│  │  └──────────────┘  └──────────────┘  └──────────────┘                     │
│  └─────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                            Service Layer                                    │
│  │                                                                             │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │  │   ESPN API   │  │  Box Score   │  │  Scoreboard  │  │ Player Stats │   │
│  │  │   Service    │  │   Service    │  │   Service    │  │   Service    │   │
│  │  │              │  │              │  │              │  │              │   │
│  │  │ • Core API   │  │ • Game       │  │ • Live       │  │ • Individual │   │
│  │  │   Calls      │  │   Details    │  │   Scores     │  │   Stats      │   │
│  │  │ • Data       │  │ • Team Stats │  │ • Schedule   │  │ • Team Stats │   │
│  │  │   Aggregation│  │ • Play Data  │  │ • Updates    │  │ • Historical │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘   │
│  │                                                                             │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │  │    Cache     │  │  Rate Limit  │  │     HTTP     │  │   Image      │   │
│  │  │   Service    │  │   Service    │  │   Service    │  │  Download    │   │
│  │  │              │  │              │  │              │  │   Service    │   │
│  │  │ • Memory     │  │ • Request    │  │ • HTTP       │  │ • Player     │   │
│  │  │   Cache      │  │   Throttling │  │   Client     │  │   Photos     │   │
│  │  │ • TTL Mgmt   │  │ • Backoff    │  │ • Resilience │  │ • Team Logos │   │
│  │  │ • Cache Keys │  │ • Monitoring │  │ • Timeouts   │  │ • File Mgmt  │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘   │
│  └─────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                          Infrastructure Layer                               │
│  │                                                                             │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │  │   Logging    │  │  Monitoring  │  │ Health Check │  │  Background  │   │
│  │  │    (Serilog) │  │   & Alerts   │  │   System     │  │     Jobs     │   │
│  │  │              │  │              │  │              │  │  (Quartz.NET)│   │
│  │  │ • Structured │  │ • Metrics    │  │ • ESPN API   │  │ • Scraping   │   │
│  │  │   Logging    │  │   Collection │  │   Health     │  │   Jobs       │   │
│  │  │ • File/      │  │ • Alert      │  │ • System     │  │ • Scheduled  │   │
│  │  │   Console    │  │   Processing │  │   Resources  │  │   Execution  │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘   │
│  └─────────────────────────────────────────────────────────────────────────────┤
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Component Interaction Matrix

| Component | Health | Metrics | Alerts | Cache | Rate Limit | HTTP | Logging |
|-----------|--------|---------|--------|-------|------------|------|---------|
| **ESPN API Service** | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **Box Score Service** | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **Scoreboard Service** | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **Player Stats Service** | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **Cache Service** | ✓ | ✓ | ✓ | - | - | - | ✓ |
| **Background Jobs** | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

---

## Data Flow Diagrams

### Primary Data Flow - API Request Processing

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           Primary Data Flow - API Request                        │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  Client Request                                                                 │
│       │                                                                         │
│       ▼                                                                         │
│  ┌─────────────────┐                                                            │
│  │  Load Balancer  │                                                            │
│  │                 │                                                            │
│  │ • Health Check  │                                                            │
│  │ • Request Route │                                                            │
│  └─────────────────┘                                                            │
│           │                                                                     │
│           ▼                                                                     │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐            │
│  │ Rate Limiting   │───▶│   Logging       │───▶│   Metrics       │            │
│  │                 │    │                 │    │                 │            │
│  │ • Check Limits  │    │ • Request ID    │    │ • Request Count │            │
│  │ • Token Bucket  │    │ • Correlation   │    │ • Response Time │            │
│  │ • Backoff       │    │ • Structured    │    │ • Error Rates   │            │
│  └─────────────────┘    └─────────────────┘    └─────────────────┘            │
│           │                                                                     │
│           ▼                                                                     │
│  ┌─────────────────┐                                                            │
│  │  Cache Check    │                                                            │
│  │                 │                                                            │
│  │ • Generate Key  │                                                            │
│  │ • Memory Cache  │                                                            │
│  │ • Distributed   │                                                            │
│  └─────────────────┘                                                            │
│           │                                                                     │
│      Cache Hit? ────────────────────────┐                                      │
│           │                             │                                      │
│       Cache Miss                   Cache Hit                                   │
│           ▼                             │                                      │
│  ┌─────────────────┐                    │                                      │
│  │  ESPN API Call  │                    │                                      │
│  │                 │                    │                                      │
│  │ • HTTP Request  │                    │                                      │
│  │ • Circuit Break │                    │                                      │
│  │ • Retry Logic   │                    │                                      │
│  │ • Timeout Mgmt  │                    │                                      │
│  └─────────────────┘                    │                                      │
│           │                             │                                      │
│           ▼                             │                                      │
│  ┌─────────────────┐                    │                                      │
│  │ Response Process│                    │                                      │
│  │                 │                    │                                      │
│  │ • Parse JSON    │                    │                                      │
│  │ • Validate Data │                    │                                      │
│  │ • Transform     │                    │                                      │
│  │ • Store in Cache│                    │                                      │
│  └─────────────────┘                    │                                      │
│           │                             │                                      │
│           └─────────────────────────────┤                                      │
│                                         ▼                                      │
│                                ┌─────────────────┐                             │
│                                │  Format Response│                             │
│                                │                 │                             │
│                                │ • Serialize     │                             │
│                                │ • Add Headers   │                             │
│                                │ • Log Response  │                             │
│                                └─────────────────┘                             │
│                                         │                                      │
│                                         ▼                                      │
│                                   Client Response                              │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Background Job Data Flow

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                        Background Job Processing Flow                            │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────┐                                                            │
│  │ Quartz.NET      │                                                            │
│  │ Scheduler       │                                                            │
│  │                 │                                                            │
│  │ • Cron Triggers │                                                            │
│  │ • Job Queue     │                                                            │
│  │ • Retry Policy  │                                                            │
│  └─────────────────┘                                                            │
│           │                                                                     │
│           ▼                                                                     │
│  ┌─────────────────┐    ┌─────────────────┐                                    │
│  │ ESPN API        │    │ Image Scraping  │                                    │
│  │ Scraping Job    │    │ Job             │                                    │
│  │                 │    │                 │                                    │
│  │ • Current Week  │    │ • Player Photos │                                    │
│  │ • Team Data     │    │ • Team Logos    │                                    │
│  │ • Player Stats  │    │ • File Download │                                    │
│  └─────────────────┘    └─────────────────┘                                    │
│           │                       │                                            │
│           ▼                       ▼                                            │
│  ┌─────────────────────────────────────────────────────────────────────────┐   │
│  │                        Job Execution Flow                               │   │
│  │                                                                         │   │
│  │  1. Pre-Execution                                                       │   │
│  │     ├── Health Check                                                    │   │
│  │     ├── Rate Limit Check                                                │   │
│  │     └── Resource Availability                                           │   │
│  │                                                                         │   │
│  │  2. Data Collection                                                     │   │
│  │     ├── ESPN API Calls                                                  │   │
│  │     ├── Parallel Processing                                             │   │
│  │     └── Error Handling                                                  │   │
│  │                                                                         │   │
│  │  3. Data Processing                                                     │   │
│  │     ├── Parse & Validate                                                │   │
│  │     ├── Transform                                                       │   │
│  │     └── Cache Storage                                                   │   │
│  │                                                                         │   │
│  │  4. File Operations                                                     │   │
│  │     ├── Save to Disk                                                    │   │
│  │     ├── Update Metadata                                                 │   │
│  │     └── Cleanup Old Files                                               │   │
│  │                                                                         │   │
│  │  5. Post-Execution                                                      │   │
│  │     ├── Update Metrics                                                  │   │
│  │     ├── Log Results                                                     │   │
│  │     └── Alert on Failures                                               │   │
│  └─────────────────────────────────────────────────────────────────────────┘   │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Cache Data Flow

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                             Cache Data Flow                                     │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────┐                                                            │
│  │ Cache Request   │                                                            │
│  │                 │                                                            │
│  │ • Generate Key  │                                                            │
│  │ • Check TTL     │                                                            │
│  └─────────────────┘                                                            │
│           │                                                                     │
│           ▼                                                                     │
│  ┌─────────────────┐                                                            │
│  │ Memory Cache    │◄─────────────────┐                                        │
│  │ (L1 Cache)      │                  │                                        │
│  │                 │                  │                                        │
│  │ • Fast Access   │                  │                                        │
│  │ • 5min TTL      │                  │                                        │
│  │ • Limited Size  │                  │                                        │
│  └─────────────────┘                  │                                        │
│           │                           │                                        │
│       Hit/Miss?                       │                                        │
│           │                           │                                        │
│       Cache Miss                      │                                        │
│           ▼                           │                                        │
│  ┌─────────────────┐                  │                                        │
│  │ Distributed     │                  │                                        │
│  │ Cache (L2)      │                  │                                        │
│  │                 │                  │                                        │
│  │ • Redis/SQL     │                  │                                        │
│  │ • Persistent    │                  │                                        │
│  │ • Configurable  │                  │                                        │
│  │   TTL           │                  │                                        │
│  └─────────────────┘                  │                                        │
│           │                           │                                        │
│       Hit/Miss?                       │                                        │
│           │                           │                                        │
│       Cache Miss                      │                                        │
│           ▼                           │                                        │
│  ┌─────────────────┐                  │                                        │
│  │ Data Source     │                  │                                        │
│  │ (ESPN API)      │                  │                                        │
│  │                 │                  │                                        │
│  │ • HTTP Request  │                  │                                        │
│  │ • Parse Response│                  │                                        │
│  │ • Validate Data │                  │                                        │
│  └─────────────────┘                  │                                        │
│           │                           │                                        │
│           ▼                           │                                        │
│  ┌─────────────────┐                  │                                        │
│  │ Cache Storage   │──────────────────┘                                        │
│  │                 │                                                            │
│  │ • Store in L2   │                                                            │
│  │ • Store in L1   │                                                            │
│  │ • Set TTL       │                                                            │
│  │ • Update Stats  │                                                            │
│  └─────────────────┘                                                            │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## Monitoring Infrastructure

### Monitoring Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                          Monitoring Infrastructure                               │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                          Data Collection Layer                              │
│  │                                                                             │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │  │   Metrics    │  │    Logs      │  │ Health Checks│  │    Traces    │   │
│  │  │  Collection  │  │  Collection  │  │              │  │              │   │
│  │  │              │  │              │  │              │  │              │   │
│  │  │ • Counters   │  │ • Serilog    │  │ • ESPN API   │  │ • Request ID │   │
│  │  │ • Histograms │  │ • Structured │  │ • System     │  │ • Correlation│   │
│  │  │ • Gauges     │  │ • File/JSON  │  │ • Database   │  │ • Timing     │   │
│  │  │ • Timers     │  │ • Correlation│  │ • Cache      │  │              │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘   │
│  └─────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                        Processing & Analysis Layer                          │
│  │                                                                             │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                     │
│  │  │    Alert     │  │  Metrics     │  │   Diagnostic │                     │
│  │  │  Processing  │  │ Aggregation  │  │   Analysis   │                     │
│  │  │              │  │              │  │              │                     │
│  │  │ • Threshold  │  │ • Time Series│  │ • Anomaly    │                     │
│  │  │   Monitoring │  │ • Statistics │  │   Detection  │                     │
│  │  │ • Correlation│  │ • Trending   │  │ • Root Cause │                     │
│  │  │ • Escalation │  │ • Baseline   │  │   Analysis   │                     │
│  │  └──────────────┘  └──────────────┘  └──────────────┘                     │
│  └─────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                           Visualization Layer                               │
│  │                                                                             │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │  │ Health Status│  │ Performance  │  │     Alerts   │  │  System Info │   │
│  │  │   Dashboard  │  │   Dashboard  │  │   Dashboard  │  │   Dashboard  │   │
│  │  │              │  │              │  │              │  │              │   │
│  │  │ • Service    │  │ • Response   │  │ • Active     │  │ • Resource   │   │
│  │  │   Status     │  │   Times      │  │   Alerts     │  │   Usage      │   │
│  │  │ • Component  │  │ • Throughput │  │ • History    │  │ • System     │   │
│  │  │   Health     │  │ • Error Rate │  │ • Severity   │  │   Status     │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘   │
│  └─────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                         API Endpoints Layer                                 │
│  │                                                                             │
│  │  GET /health          ─── Overall health status                            │
│  │  GET /metrics         ─── Performance and business metrics                 │
│  │  GET /alerts          ─── Active alerts and alert history                 │
│  │  GET /system-info     ─── System resources and runtime information        │
│  │  GET /config          ─── Current configuration and settings              │
│  │  GET /logs            ─── Recent log entries and log analysis             │
│  │  GET /full-diagnostic ─── Comprehensive diagnostic report                 │
│  └─────────────────────────────────────────────────────────────────────────────┤
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Alert Processing Flow

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                            Alert Processing Flow                                │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────┐                                                            │
│  │   Metric        │                                                            │
│  │  Collection     │                                                            │
│  │                 │                                                            │
│  │ • API Response  │                                                            │
│  │   Time          │                                                            │
│  │ • Cache Hit Rate│                                                            │
│  │ • Error Rate    │                                                            │
│  │ • Memory Usage  │                                                            │
│  └─────────────────┘                                                            │
│           │                                                                     │
│           ▼                                                                     │
│  ┌─────────────────┐                                                            │
│  │  Threshold      │                                                            │
│  │  Evaluation     │                                                            │
│  │                 │                                                            │
│  │ • Compare with  │                                                            │
│  │   Thresholds    │                                                            │
│  │ • Time Window   │                                                            │
│  │ • Trending      │                                                            │
│  └─────────────────┘                                                            │
│           │                                                                     │
│     Threshold                                                                   │
│     Exceeded?                                                                   │
│           │                                                                     │
│           ▼                                                                     │
│  ┌─────────────────┐                                                            │
│  │ Alert Creation  │                                                            │
│  │                 │                                                            │
│  │ • Generate ID   │                                                            │
│  │ • Set Severity  │                                                            │
│  │ • Timestamp     │                                                            │
│  │ • Context Data  │                                                            │
│  └─────────────────┘                                                            │
│           │                                                                     │
│           ▼                                                                     │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐            │
│  │ Alert Storage   │───▶│ Alert Routing   │───▶│   Notification  │            │
│  │                 │    │                 │    │                 │            │
│  │ • Active Alerts │    │ • Severity      │    │ • Log Entry     │            │
│  │ • Alert History │    │   Based         │    │ • API Response  │            │
│  │ • State Mgmt    │    │ • Component     │    │ • External      │            │
│  │                 │    │   Based         │    │   Systems       │            │
│  └─────────────────┘    └─────────────────┘    └─────────────────┘            │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## Deployment Architecture

### Container Deployment

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           Container Deployment Architecture                      │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                         Docker Container Layer                              │
│  │                                                                             │
│  │  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  │                     ESPN API Service Container                       │   │
│  │  │                                                                      │   │
│  │  │  Base Image: mcr.microsoft.com/dotnet/aspnet:8.0                    │   │
│  │  │                                                                      │   │
│  │  │  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐        │   │
│  │  │  │   Application  │  │  Configuration │  │    Runtime     │        │   │
│  │  │  │     Layer      │  │     Layer      │  │     Layer      │        │   │
│  │  │  │                │  │                │  │                │        │   │
│  │  │  │ • .NET 8.0     │  │ • appsettings  │  │ • .NET Runtime │        │   │
│  │  │  │ • ESPN API     │  │ • Environment  │  │ • Dependencies │        │   │
│  │  │  │   Service      │  │   Variables    │  │ • Health Check │        │   │
│  │  │  │ • Controllers  │  │ • Secrets      │  │ • Monitoring   │        │   │
│  │  │  │ • Services     │  │ • Certificates │  │               │        │   │
│  │  │  └────────────────┘  └────────────────┘  └────────────────┘        │   │
│  │  │                                                                      │   │
│  │  │  Exposed Ports: 80 (HTTP), 443 (HTTPS)                             │   │
│  │  │  Health Check: GET /health (30s interval)                           │   │
│  │  │  Resource Limits: 512MB RAM, 1 CPU                                  │   │
│  │  └──────────────────────────────────────────────────────────────────────┘   │
│  └─────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                            Volume Mounts                                    │
│  │                                                                             │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │  │     Logs     │  │   Downloads  │  │     Config   │  │   Secrets    │   │
│  │  │              │  │              │  │              │  │              │   │
│  │  │ Host: ./logs │  │Host: ./down  │  │Host: ./conf  │  │Host: ./sec   │   │
│  │  │Container:    │  │loads         │  │Container:    │  │Container:    │   │
│  │  │ /app/logs    │  │Container:    │  │ /app/config  │  │ /app/secrets │   │
│  │  │              │  │ /app/down    │  │              │  │              │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘   │
│  └─────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                        Network Configuration                                │
│  │                                                                             │
│  │  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  │                        Bridge Network                                │   │
│  │  │                                                                      │   │
│  │  │  Container IP: 172.17.0.2/16                                        │   │
│  │  │  Port Mapping: 5000:80 (host:container)                             │   │
│  │  │  DNS: Custom (for ESPN API resolution)                              │   │
│  │  │                                                                      │   │
│  │  │  External Connectivity:                                              │   │
│  │  │  • ESPN API (sports.core.api.espn.com)                              │   │
│  │  │  • Health Check Endpoints                                           │   │
│  │  │  • Monitoring Systems                                                │   │
│  │  └──────────────────────────────────────────────────────────────────────┘   │
│  └─────────────────────────────────────────────────────────────────────────────┤
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Multi-Environment Deployment

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                        Multi-Environment Deployment                             │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                           Development Environment                            │
│  │                                                                             │
│  │  • Local Docker Compose                                                    │
│  │  • Hot Reload Enabled                                                      │
│  │  • Debug Configuration                                                     │
│  │  • Relaxed Security                                                        │
│  │  • Verbose Logging                                                         │
│  │  • Mock ESPN API (Optional)                                                │
│  │                                                                             │
│  │  Resources: 256MB RAM, 0.5 CPU                                             │
│  │  Monitoring: Basic health checks                                           │
│  └─────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                            Staging Environment                              │
│  │                                                                             │
│  │  • Production-like Container                                               │
│  │  • Full Monitoring Enabled                                                 │
│  │  • Production Configuration                                                │
│  │  • SSL Certificates                                                        │
│  │  • Load Testing Capable                                                    │
│  │  • Real ESPN API                                                           │
│  │                                                                             │
│  │  Resources: 512MB RAM, 1.0 CPU                                             │
│  │  Monitoring: Full diagnostic suite                                         │
│  └─────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                          Production Environment                             │
│  │                                                                             │
│  │  • Optimized Container Image                                               │
│  │  • High Availability Setup                                                 │
│  │  • Enterprise Security                                                     │
│  │  • Comprehensive Monitoring                                                │
│  │  • Automated Scaling                                                       │
│  │  • Disaster Recovery                                                       │
│  │                                                                             │
│  │  Resources: 1GB RAM, 2.0 CPU (scalable)                                   │
│  │  Monitoring: Enterprise observability                                      │
│  └─────────────────────────────────────────────────────────────────────────────┤
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## Security Architecture

### Security Layers

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                             Security Architecture                               │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                           Network Security                                   │
│  │                                                                             │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │  │   Firewall   │  │     SSL/TLS  │  │   Load Bal   │  │    WAF       │   │
│  │  │              │  │              │  │              │  │              │   │
│  │  │ • Port       │  │ • Cert Mgmt  │  │ • Health     │  │ • Request    │   │
│  │  │   Filtering  │  │ • Encryption │  │   Checks     │  │   Filtering  │   │
│  │  │ • IP Allow   │  │ • TLS 1.3    │  │ • Rate Limit │  │ • DDoS Prot  │   │
│  │  │   Lists      │  │              │  │              │  │              │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘   │
│  └─────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                         Application Security                                │
│  │                                                                             │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │  │Input Valid   │  │  Auth/Authz  │  │   Logging    │  │   Secrets    │   │
│  │  │              │  │              │  │              │  │              │   │
│  │  │ • Parameter  │  │ • API Keys   │  │ • Audit Trail│  │ • Key Vault  │   │
│  │  │   Validation │  │ • RBAC       │  │ • Security   │  │ • Encryption │   │
│  │  │ • Data       │  │ • JWT Tokens │  │   Events     │  │ • Rotation   │   │
│  │  │   Sanitization│  │              │  │ • Compliance │  │              │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘   │
│  └─────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                         Infrastructure Security                             │
│  │                                                                             │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │  │   Container  │  │   Runtime    │  │   Network    │  │   Monitoring │   │
│  │  │   Security   │  │   Security   │  │   Isolation  │  │   Security   │   │
│  │  │              │  │              │  │              │  │              │   │
│  │  │ • Base Image │  │ • Non-Root   │  │ • Private    │  │ • SIEM       │   │
│  │  │   Scanning   │  │   User       │  │   Networks   │  │ • Alert      │   │
│  │  │ • Vuln Mgmt  │  │ • Resource   │  │ • VPC        │  │   Correlation│   │
│  │  │ • Compliance │  │   Limits     │  │ • Segmentation│  │ • Incident   │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘   │
│  └─────────────────────────────────────────────────────────────────────────────┤
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Security Controls Matrix

| Layer | Control | Implementation | Monitoring |
|-------|---------|----------------|------------|
| **Network** | Firewall | iptables/Cloud Security Groups | Connection logs |
| **Network** | SSL/TLS | Certificate management | SSL monitoring |
| **Application** | Input Validation | Data annotations, custom validators | Validation failures |
| **Application** | Rate Limiting | Token bucket, sliding window | Rate limit metrics |
| **Application** | Authentication | API keys, JWT tokens | Auth events |
| **Application** | Authorization | Role-based access control | Access logs |
| **Infrastructure** | Container Security | Image scanning, runtime protection | Security events |
| **Infrastructure** | Secrets Management | Key vault, environment variables | Secret access |

---

## Scaling and Performance

### Horizontal Scaling Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                          Horizontal Scaling Architecture                        │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                           Load Balancer Layer                               │
│  │                                                                             │
│  │  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  │                      Application Load Balancer                       │   │
│  │  │                                                                      │   │
│  │  │  • Health Check: GET /health (every 30s)                            │   │
│  │  │  • Algorithm: Round Robin / Least Connections                       │   │
│  │  │  • Session Affinity: None (stateless)                               │   │
│  │  │  • SSL Termination: Yes                                              │   │
│  │  │  • Rate Limiting: 1000 req/min per IP                               │   │
│  │  └──────────────────────────────────────────────────────────────────────┘   │
│  └─────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                         Application Instance Layer                          │
│  │                                                                             │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │  │ Instance 1   │  │ Instance 2   │  │ Instance 3   │  │ Instance N   │   │
│  │  │              │  │              │  │              │  │              │   │
│  │  │ • Stateless  │  │ • Stateless  │  │ • Stateless  │  │ • Auto-scale │   │
│  │  │ • Health OK  │  │ • Health OK  │  │ • Health OK  │  │ • On-demand  │   │
│  │  │ • Cache L1   │  │ • Cache L1   │  │ • Cache L1   │  │ • Dynamic    │   │
│  │  │ • Local Logs │  │ • Local Logs │  │ • Local Logs │  │   Config     │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘   │
│  └─────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┤
│  │                           Shared Services Layer                             │
│  │                                                                             │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │  │ Distributed  │  │   Central    │  │   Shared     │  │   External   │   │
│  │  │    Cache     │  │   Logging    │  │   Config     │  │   Services   │   │
│  │  │              │  │              │  │              │  │              │   │
│  │  │ • Redis      │  │ • Log        │  │ • Config     │  │ • ESPN API   │   │
│  │  │ • Consistent │  │   Aggregation│  │   Server     │  │ • Monitoring │   │
│  │  │ • High Avail │  │ • ELK Stack  │  │ • Key Vault  │  │ • Alerting   │   │
│  │  │              │  │ • Retention  │  │ • Environment│  │              │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘   │
│  └─────────────────────────────────────────────────────────────────────────────┤
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Auto-Scaling Triggers

| Metric | Scale Out Threshold | Scale In Threshold | Cooldown |
|--------|--------------------|--------------------|----------|
| **CPU Usage** | > 70% for 5 minutes | < 30% for 10 minutes | 5 minutes |
| **Memory Usage** | > 80% for 3 minutes | < 40% for 10 minutes | 5 minutes |
| **Request Rate** | > 80% capacity | < 40% capacity | 3 minutes |
| **Response Time** | > 1000ms average | < 300ms average | 5 minutes |
| **Error Rate** | > 5% for 2 minutes | < 1% for 5 minutes | 10 minutes |

### Performance Optimization Points

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                         Performance Optimization Points                         │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  1. Application Layer Optimizations                                            │
│     ├── Async/Await Patterns                                                   │
│     ├── Connection Pooling                                                     │
│     ├── Request Batching                                                       │
│     ├── Response Compression                                                   │
│     └── Memory Management                                                      │
│                                                                                 │
│  2. Caching Optimizations                                                      │
│     ├── Multi-Layer Cache Strategy                                             │
│     ├── Cache-Aside Pattern                                                    │
│     ├── Write-Through Caching                                                  │
│     ├── TTL Optimization                                                       │
│     └── Cache Warming                                                          │
│                                                                                 │
│  3. Database/Storage Optimizations                                             │
│     ├── Query Optimization                                                     │
│     ├── Index Strategies                                                       │
│     ├── Connection Pooling                                                     │
│     ├── Read Replicas                                                          │
│     └── Data Partitioning                                                      │
│                                                                                 │
│  4. Network Optimizations                                                      │
│     ├── HTTP/2 Support                                                         │
│     ├── Keep-Alive Connections                                                 │
│     ├── Content Compression                                                    │
│     ├── CDN Integration                                                        │
│     └── DNS Optimization                                                       │
│                                                                                 │
│  5. Infrastructure Optimizations                                               │
│     ├── Container Right-Sizing                                                 │
│     ├── Resource Limits                                                        │
│     ├── JIT Compilation                                                        │
│     ├── Garbage Collection Tuning                                              │
│     └── OS-Level Optimizations                                                 │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## Technology Stack Summary

### Core Technologies
- **.NET 8.0**: Latest long-term support framework
- **ASP.NET Core**: Web API framework with built-in health checks
- **Serilog**: Structured logging with multiple sinks
- **Quartz.NET**: Enterprise job scheduling

### Infrastructure
- **Docker**: Containerization platform
- **Redis**: Distributed caching (optional)
- **Nginx**: Load balancing and reverse proxy
- **Let's Encrypt**: SSL certificate management

### Monitoring & Observability
- **Application Insights**: Performance monitoring
- **Prometheus**: Metrics collection
- **Grafana**: Visualization dashboards
- **ELK Stack**: Log aggregation and analysis

### Security
- **Key Vault**: Secrets management
- **OAuth 2.0/JWT**: Authentication and authorization
- **HTTPS**: Encrypted communication
- **WAF**: Web application firewall

---

For implementation details and operational procedures, see:
- [API Documentation](API_DOCUMENTATION.md)
- [Operational Runbooks](OPERATIONAL_RUNBOOKS.md)
- [Performance Tuning Guide](PERFORMANCE_TUNING.md)
- [Deployment Scripts](deployment/)