# PROJECT_SPECIFICATION.md

# DePIN Tracker -- Project Specification

## Vision

Build a production-grade, offline-first, portable Windows desktop
application for tracking DePIN rewards, managing wallets, producing
analytics, and generating reproducible tax reports.

## Core Principles

-   Offline-first
-   Portable (no installer, no AppData, no registry)
-   Clean Architecture
-   Generic (no project-specific logic)
-   Extensible through plugins
-   Deterministic and reproducible calculations
-   Auditability over convenience
-   Database rebuildable from online sources
-   Long-term maintainability

## Functional Requirements

### Project Management

-   Unlimited projects
-   Unlimited wallets
-   Multiple wallets per project
-   Wallet activation/deactivation
-   Notes and tags

### Blockchain Support

Plugin-based support for Polygon, Solana, Cosmos, Ethereum, Arbitrum,
Base, Optimism, BNB and future chains.

### Reward Tracking

-   Import reward transactions
-   Preserve raw provider responses
-   Store transaction hash, block, provider, import session
-   Never silently overwrite imported financial data

### Price Engine

-   Historical token prices
-   Multiple providers (CoinGecko, DexScreener, CoinPaprika, CSV,
    custom)
-   Local cache
-   Versioned imports

### Exchange Rates

-   Historical fiat exchange rates
-   Multiple providers (ECB, Frankfurter API, CSV, custom)

### Tax

-   Historical valuation
-   Yearly reports
-   Excel, CSV and PDF export
-   FIFO-ready architecture

### Analytics

-   Daily, monthly, yearly rewards
-   Portfolio
-   Holdings
-   Average acquisition value
-   Wallet/project statistics

### Dashboard

-   Portfolio value
-   Rewards
-   Wallet count
-   Project count
-   Missing prices
-   Synchronization state
-   Database health

### Database

-   SQLite + Dapper
-   Versioned schema
-   Separation of:
    -   Configuration
    -   Imported Data
    -   Generated Data

### Rebuild Engine

Support rebuilding: - Entire database - Wallet - Project - Blockchain -
Year - Price data - Exchange rates

### Backup & Restore

-   Full backup
-   Restore
-   Integrity verification

### Plugin System

Interfaces: - IBlockchainExplorer - IPriceProvider -
IExchangeRateProvider - IRewardClassifier - IAnalyticsProvider -
IReportExporter

### Portable Structure

config/ data/ cache/ logs/ exports/ plugins/ backups/

### Architecture

-   .NET 9
-   WPF
-   MVVM
-   Clean Architecture
-   Dependency Injection
-   Async-first
-   CancellationToken everywhere

### Dependencies

-   Microsoft.Data.Sqlite
-   Dapper
-   System.Text.Json
-   HttpClient
-   Microsoft.Extensions.\*
-   Open XML SDK
-   ScottPlot

### Testing

-   xUnit
-   FluentAssertions
-   Moq
-   GitHub Actions
-   Code coverage

### Documentation

README ARCHITECTURE DATABASE PLUGIN_GUIDE ROADMAP CHANGELOG CONTRIBUTING
SECURITY

### Licensing

Recommend Business Source License 1.1 (BSL) to allow public source while
preserving future commercial licensing options.

## Guiding Principle

Maintainability, extensibility, auditability, deterministic behaviour
and reproducibility always take precedence over implementation
simplicity.
