# EconAdmin

A comprehensive admin toolkit for managing currencies and bank accounts on [ECO](https://play.eco/) servers.

## Features

- List all bank accounts and currencies on the server
- Search/filter accounts and currencies by name
- Adjust currency balances on specific accounts
- Wipe a specific currency from a specific account
- Bulk purge currencies from **all** accounts using wildcard patterns
- Preview wildcard matches before executing destructive operations

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

## Example Usage

```
/ea-currencies
/ea-accounts PlayerName
/ea-balance "PlayerName's Account"
/ea-adjust "PlayerName's Account" Credits 500
/ea-preview *OldCoin*
/ea-purge *OldCoin*
```

## License

MIT — see [LICENSE](LICENSE)
