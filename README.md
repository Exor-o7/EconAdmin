# EconAdmin

A comprehensive admin toolkit for managing currencies and bank accounts on [ECO](https://play.eco/) servers.

## Features

### Economy Administration (`/ea`)
- List all bank accounts and currencies on the server (with numeric IDs for precise targeting)
- Search/filter accounts and currencies by name or ID
- Add or deduct currency balances on specific accounts
- Wipe a specific currency from a specific account
- Delete a bank account entirely
- Bulk purge currencies from **all** accounts using wildcard patterns
- Preview wildcard matches before executing destructive operations

### Global Currency Management (`/eagc`)
- Create a server-wide currency with a treasury account
- Automatically gift new players a starting balance on first join
- Configurable welcome popup panel for new players
- Mint additional funds into the treasury at any time

## Installation

Simply copy the contents of this repository into your ECO server's root directory. The folder structure mirrors the server layout exactly:

```
ServerRoot/
├── Configs/
│   └── EconAdmin.eco.template  ← rename to EconAdmin.eco and configure
└── Mods/
    └── UserCode/
        └── EconAdmin.cs        ← mod file
```

1. Copy `Configs/EconAdmin.eco.template` → your server's `Configs/` folder and **make a copy of it and rename to `EconAdmin.eco`**.
   > Alternatively, skip this step — the server will auto-generate `EconAdmin.eco` with default values on first start.
2. Copy `Mods/UserCode/EconAdmin.cs` → your server's `Mods/UserCode/` folder.
3. Edit `Configs/EconAdmin.eco` with your desired settings (see Global Currency Setup below).
4. Restart the server — the mod will load automatically.
5. Commands are available in-game to admins.

## Commands

All commands are grouped under two master commands:

### `/ea` — General economy administration

| Command | Description |
|---|---|
| `/ea accounts [search]` | List all bank accounts with IDs, with optional search filter |
| `/ea currencies [filter]` | List all currencies with IDs, with optional filter |
| `/ea balance <account>` | Show all currency holdings for an account |
| `/ea add <account> <currency> <amount>` | Add currency to an account |
| `/ea deduct <account> <currency> <amount>` | Deduct currency from an account |
| `/ea wipe <account> <currency>` | Remove ALL of a specific currency from one account |
| `/ea delete <account>` | **⚠ DANGEROUS** — Permanently delete a bank account |
| `/ea preview <pattern>` | Preview which currencies match a wildcard pattern |
| `/ea purge <pattern>` | **⚠ DANGEROUS** — Remove matching currencies from ALL accounts |

### `/eagc` — Global currency management

| Command | Description |
|---|---|
| `/eagc status` | Show global currency config and treasury status |
| `/eagc create` | Create the global currency and treasury from config |
| `/eagc gift <account> [amount]` | Gift global currency to an account (defaults to configured gift amount) |
| `/eagc mint <amount>` | Mint additional global currency into the treasury |

> All commands require **Admin** authorization.

## Account & Currency Lookup

All commands that take an `<account>` or `<currency>` argument support three lookup methods, tried in order:
1. **Numeric ID** — use the ID shown in `/ea accounts` or `/ea currencies` (most reliable, e.g. `/ea balance 42`)
2. **Exact name** — full name match (case-insensitive)
3. **Partial name** — substring match (case-insensitive)

> Tip: If a name contains spaces, use the numeric ID to avoid ambiguity.

## Wildcard Patterns

The `/ea preview` and `/ea purge` commands support `*` as a wildcard:

| Pattern | Matches |
|---|---|
| `*Credit` | Anything ending with "Credit" |
| `Player*` | Anything starting with "Player" |
| `*Old*` | Anything containing "Old" |
| `ExactName` | Exact name match only |

**Always use `/ea preview` before `/ea purge` to verify what will be affected.**

## Global Currency Setup

1. Edit `Configs/EconAdmin.eco` (rename from `EconAdmin.eco.template`) with your values:
   ```json
   {
     "GlobalCurrencyName": "Gold",
     "TreasuryAccountName": "",
     "NewPlayerGiftAmount": 500,
     "TreasuryInitialBalance": 1000000,
     "WelcomePanelTitle": "Welcome!",
     "WelcomePanelBody": "You have been given $Amount Gold to get started!"
   }
   ```
   | Field | Description |
   |---|---|
   | `GlobalCurrencyName` | Name of your server's global currency |
   | `TreasuryAccountName` | Name of the treasury bank account. Leave empty to default to `CurrencyName - Treasury` |
   | `NewPlayerGiftAmount` | Amount gifted to new players on first join. Set to `0` to disable |
   | `TreasuryInitialBalance` | Funds seeded into the treasury when created via `/eagc create` |
   | `WelcomePanelTitle` | Title of the welcome popup. Leave empty to skip |
   | `WelcomePanelBody` | Body of the welcome popup. Use `$Amount` as a placeholder for the gift value |
2. Place the files on the server and run `/eagc create` in-game to create the currency and treasury.
3. Use `/eagc status` to verify everything is running.

> If you are migrating from the GlobalCurrency mod, your existing `CurrencyName - Treasury` account will be recognized automatically.

## Example Usage

```
/ea currencies
/ea accounts PlayerName
/ea balance 42
/ea add 42 Credits 500
/ea deduct 42 Credits 100
/ea wipe 42 Credits
/ea delete 42
/ea preview *OldCoin*
/ea purge *OldCoin*
/eagc create
/eagc status
/eagc gift 42 250
/eagc mint 50000
```

> Arguments containing spaces are best referenced by their numeric ID (shown in `/ea accounts` and `/ea currencies`).

## License

MIT — see [LICENSE](LICENSE)

