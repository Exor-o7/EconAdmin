# EconAdmin

A comprehensive admin toolkit for managing currencies and bank accounts on [ECO](https://play.eco/) servers.

## Features

- List all bank accounts and currencies on the server
- Search/filter accounts and currencies by name
- Adjust currency balances on specific accounts
- Wipe a specific currency from a specific account
- Bulk purge currencies from **all** accounts using wildcard patterns
- Preview wildcard matches before executing destructive operations
- Global currency system: create a server-wide currency with a treasury account
- Automatically gift new players a starting balance on first join
- Configurable welcome popup panel for new players
- Mint additional funds into the treasury at any time

## Installation

1. Drop `EconAdmin.cs` into your ECO server's `Mods/UserCode/` folder.
2. Restart the server (or use a live-reload mod).
3. Commands are available in-game to admins.

## Commands

| Command | Description |
|---|---|
| `/ea-accounts [search]` | List all bank accounts, with optional search filter |
| `/ea-currencies [filter]` | List all currencies, with optional filter |
| `/ea-balance <account>` | Show all currency holdings for an account |
| `/ea-adjust <account> <currency> <amount>` | Add or remove currency from an account (use negative to remove) |
| `/ea-wipe <account> <currency>` | Remove ALL of a specific currency from one account |
| `/ea-preview <pattern>` | Preview which currencies match a wildcard pattern |
| `/ea-purge <pattern>` | **⚠ DANGEROUS** — Remove matching currencies from ALL accounts |
| `/ea-gc-status` | Show global currency config and treasury status |
| `/ea-gc-setup` | Create the global currency and treasury from config |
| `/ea-gc-gift <account> [amount]` | Gift global currency to an account (defaults to configured gift amount) |
| `/ea-gc-mint <amount>` | Mint additional global currency into the treasury |

> All commands require **Admin** authorization.

## Wildcard Patterns

The `ea-preview` and `ea-purge` commands support `*` as a wildcard:

| Pattern | Matches |
|---|---|
| `*Credit` | Anything ending with "Credit" |
| `Player*` | Anything starting with "Player" |
| `*Old*` | Anything containing "Old" |
| `ExactName` | Exact name match only |

**Always use `/ea-preview` before `/ea-purge` to verify what will be affected.**

## Global Currency Setup

1. Edit `Configs/EconAdmin.eco` on your server (auto-created on first run):
   ```json
   {
     "GlobalCurrencyName": "Credits",
     "NewPlayerGiftAmount": 500,
     "TreasuryInitialBalance": 1000000,
     "WelcomePanelTitle": "Welcome!",
     "WelcomePanelBody": "You have been given $Amount Credits to get started."
   }
   ```
2. Run `/ea-gc-setup` in-game to create the currency and treasury (`CurrencyName - Treasury`).
3. Use `/ea-gc-status` to verify everything is running.

> If you are migrating from the GlobalCurrency mod, your existing `CurrencyName - Treasury` account will be recognized automatically.

## Example Usage

```
/ea-currencies
/ea-accounts PlayerName
/ea-balance "PlayerName's Account"
/ea-adjust "PlayerName's Account" Credits 500
/ea-preview *OldCoin*
/ea-purge *OldCoin*
/ea-gc-status
/ea-gc-setup
/ea-gc-gift "PlayerName's Account" 250
/ea-gc-mint 50000
```

## License

MIT — see [LICENSE](LICENSE)
