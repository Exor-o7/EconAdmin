// EconAdmin
// A comprehensive admin toolkit for managing currencies and bank accounts on ECO servers
// Author: Exor-o7
// Features: Currency/account listing, bulk operations, wildcard pattern matching, global currency management

using Eco.Core.Plugins;
using Eco.Core.Plugins.Interfaces;
using Eco.Core.Utils;
using Eco.Gameplay.Economy;
using Eco.Gameplay.Items;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.Messaging.Chat.Commands;
using Eco.Shared.Items;
using Eco.Shared.Localization;
using Eco.Shared.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Eco.Mods.EconAdmin
{
    // ----------------------------
    // Global Currency Config
    // ----------------------------

    [Localized]
    public class EconAdminConfig : Singleton<EconAdminConfig>
    {
        [LocDescription("Name of the global currency managed by EconAdmin.")]
        public string GlobalCurrencyName { get; set; } = "";

        [LocDescription("Amount of global currency gifted to players on their very first join. Set to 0 to disable.")]
        public int NewPlayerGiftAmount { get; set; } = 0;

        [LocDescription("Name of the bank account used as the global currency treasury. Defaults to 'CurrencyName - Treasury' if left empty.")]
        public string TreasuryAccountName { get; set; } = "";

        [LocDescription("Starting balance deposited into the treasury when it is first created via /ea-gc create.")]
        public int TreasuryInitialBalance { get; set; } = 1000000;

        [LocDescription("Title of the popup panel shown to new players receiving a starting gift. Leave empty to skip.")]
        public string WelcomePanelTitle { get; set; } = "";

        [LocDescription("Body text of the welcome panel. Use $Amount as a placeholder for the gift value.")]
        public string WelcomePanelBody { get; set; } = "";
    }

    // ----------------------------
    // EconAdmin Plugin
    // ----------------------------

    public class EconAdminPlugin : IModKitPlugin, IInitializablePlugin, IConfigurablePlugin
    {
        internal static PluginConfig<EconAdminConfig>? Config;
        private string status = "Idle";

        public IPluginConfig? PluginConfig => Config;
        public ThreadSafeAction<object, string> ParamChanged { get; set; } = new ThreadSafeAction<object, string>();

        public void Initialize(TimedTask timer)
        {
            Config = new PluginConfig<EconAdminConfig>("EconAdmin");
            UserManager.NewUserJoinedEvent.Add(GrantNewPlayerGift);
            this.status = "Running";
        }

        /// <summary>Resolve the treasury account name: use configured value or fall back to 'CurrencyName - Treasury'</summary>
        internal static string ResolveTreasuryName(EconAdminConfig cfg)
        {
            return !string.IsNullOrWhiteSpace(cfg.TreasuryAccountName)
                ? cfg.TreasuryAccountName
                : cfg.GlobalCurrencyName + " - Treasury";
        }

        public override string ToString() => "EconAdmin";
        public string GetStatus() => this.status;
        public string GetCategory() => "EconAdmin";
        public object? GetEditObject() => Config?.Config;
        public void OnEditObjectChanged(object o, string param) { }

        private static void GrantNewPlayerGift(User user)
        {
            if (Config?.Config == null) return;
            var cfg = Config.Config;

            if (cfg.NewPlayerGiftAmount <= 0 || string.IsNullOrEmpty(cfg.GlobalCurrencyName)) return;

            var currency = CurrencyManager.Currencies
                .FirstOrDefault(c => c != null && c.Name.Equals(cfg.GlobalCurrencyName, StringComparison.OrdinalIgnoreCase));

            if (currency == null) return;

            BankAccountManager.Obj.SpawnMoney(currency, user, (float)cfg.NewPlayerGiftAmount);

            if (user.Player != null && !string.IsNullOrEmpty(cfg.WelcomePanelTitle) && !string.IsNullOrEmpty(cfg.WelcomePanelBody))
            {
                var body = cfg.WelcomePanelBody.Replace("$Amount", cfg.NewPlayerGiftAmount.ToString());
                user.Player.OpenInfoPanel(cfg.WelcomePanelTitle, body, "EconAdmin");
            }
        }
    }

    [ChatCommandHandler]
    public static class EconAdminCommands
    {
        // ----------------------------
        // Helper Methods
        // ----------------------------

        /// <summary>Get a currency by exact name match</summary>
        private static Currency? GetCurrencyByName(string currencyName)
        {
            return CurrencyManager.Currencies.FirstOrDefault(c => c != null &&
                c.Name.Equals(currencyName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Strip quotes that ECO's parser may leave on tokens when the user types quoted strings</summary>
        private static string StripQuotes(string s) => s.Trim().Trim('"').Trim();

        /// <summary>
        /// Resolve a bank account by numeric ID, exact name, or partial/contains match.
        /// Prints an error or ambiguity message and returns null if unresolved.
        /// </summary>
        private static BankAccount? ResolveAccount(User admin, string search)
        {
            search = StripQuotes(search);
            if (string.IsNullOrWhiteSpace(search)) return null;

            // Try numeric ID lookup first
            if (int.TryParse(search, out int id))
            {
                var byId = BankAccountManager.Obj.Accounts.FirstOrDefault(a => a != null && a.Id == id);
                if (byId != null) return byId;
                admin.TempServerMessage(Localizer.DoStr($"[EA] No account found with ID {id}."));
                return null;
            }

            var exact = BankAccountManager.Obj.Accounts
                .FirstOrDefault(a => a != null && a.Name != null &&
                                     a.Name.Equals(search, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            var matches = BankAccountManager.Obj.Accounts
                .Where(a => a != null && a.Name != null &&
                            a.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (matches.Count == 0)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] No account found matching '{search}'. Use /ea accounts to search."));
                return null;
            }
            if (matches.Count == 1) return matches[0];

            admin.TempServerMessage(Localizer.DoStr($"[EA] '{search}' matches {matches.Count} accounts — use the ID# or be more specific:"));
            foreach (var a in matches.Take(10))
                admin.TempServerMessage(Localizer.DoStr($"  • [{a.Id}] {a.Name}"));
            return null;
        }

        /// <summary>
        /// Resolve a currency by numeric ID, exact name, or partial/contains match.
        /// Prints an error or ambiguity message and returns null if unresolved.
        /// </summary>
        private static Currency? ResolveCurrency(User admin, string search)
        {
            search = StripQuotes(search);
            if (string.IsNullOrWhiteSpace(search)) return null;

            // Try numeric ID lookup first
            if (int.TryParse(search, out int id))
            {
                var byId = CurrencyManager.Currencies.FirstOrDefault(c => c != null && c.Id == id);
                if (byId != null) return byId;
                admin.TempServerMessage(Localizer.DoStr($"[EA] No currency found with ID {id}."));
                return null;
            }

            var exact = CurrencyManager.Currencies
                .FirstOrDefault(c => c != null && c.Name != null &&
                                     c.Name.Equals(search, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            var matches = CurrencyManager.Currencies
                .Where(c => c != null && c.Name != null &&
                            c.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (matches.Count == 0)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] No currency found matching '{search}'. Use /ea currencies to see all."));
                return null;
            }
            if (matches.Count == 1) return matches[0];

            admin.TempServerMessage(Localizer.DoStr($"[EA] '{search}' matches {matches.Count} currencies — use the ID# or be more specific:"));
            foreach (var c in matches.Take(10))
                admin.TempServerMessage(Localizer.DoStr($"  • [{c.Id}] {c.Name}"));
            return null;
        }

        /// <summary>Check if a currency name matches a wildcard pattern (* wildcard)</summary>
        private static bool MatchesWildcard(string currencyName, string pattern)
        {
            int starCount = pattern.Count(c => c == '*');

            if (starCount == 0)
                return currencyName.Equals(pattern, StringComparison.OrdinalIgnoreCase);

            if (starCount == 1)
            {
                int starPos = pattern.IndexOf('*');

                // *suffix
                if (starPos == 0)
                {
                    string suffix = pattern.Substring(1).TrimStart();
                    if (currencyName.EndsWith(" " + suffix, StringComparison.OrdinalIgnoreCase))
                        return true;
                    return currencyName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
                }

                // prefix*
                if (starPos == pattern.Length - 1)
                {
                    string prefix = pattern.Substring(0, pattern.Length - 1).TrimEnd();
                    if (currencyName.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
                        return true;
                    return currencyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
                }

                // prefix*suffix
                string prefixPart = pattern.Substring(0, starPos);
                string suffixPart = pattern.Substring(starPos + 1);
                return currencyName.StartsWith(prefixPart, StringComparison.OrdinalIgnoreCase) &&
                       currencyName.EndsWith(suffixPart, StringComparison.OrdinalIgnoreCase) &&
                       currencyName.Length >= prefixPart.Length + suffixPart.Length;
            }

            if (starCount == 2 && pattern.StartsWith("*") && pattern.EndsWith("*"))
            {
                // *contains*
                string contains = pattern.Substring(1, pattern.Length - 2).Trim();
                return currencyName.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            // Fallback
            string simplified = pattern.Replace("*", "").Trim();
            return currencyName.IndexOf(simplified, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Get all currencies matching a pattern (* wildcard)</summary>
        private static List<Currency> GetCurrenciesByPattern(string pattern)
        {
            if (!pattern.Contains("*"))
            {
                var currency = GetCurrencyByName(pattern);
                return currency != null ? new List<Currency> { currency } : new List<Currency>();
            }

            return CurrencyManager.Currencies
                .Where(c => c != null && c.Name != null && MatchesWildcard(c.Name, pattern))
                .ToList();
        }

        // ----------------------------
        // Master Commands
        // ----------------------------

        [ChatCommand("Economy administration toolkit", ChatAuthorizationLevel.Admin)]
        public static void Ea(User user) { }

        [ChatCommand("Global currency management", ChatAuthorizationLevel.Admin)]
        public static void EaGc(User user) { }

        // ----------------------------
        // ea subcommands
        // ----------------------------

        [ChatSubCommand("Ea", "List all bank accounts (optional: search filter)", "accounts", ChatAuthorizationLevel.Admin)]
        public static void Accounts(User admin, string search = "")
        {
            search = search ?? string.Empty;

            var accounts = BankAccountManager.Obj.Accounts
                .Where(a => a != null && a.Name != null &&
                            (string.IsNullOrEmpty(search) ||
                             a.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                .Take(100)
                .ToList();

            if (accounts.Count == 0)
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] No matching accounts found."));
                return;
            }

            admin.TempServerMessage(Localizer.DoStr($"[EA] Found {accounts.Count} account(s):"));
            foreach (var a in accounts)
                admin.TempServerMessage(Localizer.DoStr($"  • [{a.Id}] {a.Name}"));
        }

        [ChatSubCommand("Ea", "List all currencies in the system (optional: filter)", "currencies", ChatAuthorizationLevel.Admin)]
        public static void Currencies(User admin, string filter = "")
        {
            var currencies = CurrencyManager.Currencies
                .Where(c => c != null && c.Name != null &&
                           (string.IsNullOrEmpty(filter) ||
                            c.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            if (currencies.Count == 0)
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] No currencies found."));
                return;
            }

            admin.TempServerMessage(Localizer.DoStr($"[EA] Total: {currencies.Count} currencies"));
            foreach (var curr in currencies.Take(100))
                admin.TempServerMessage(Localizer.DoStr($"  • [{curr.Id}] {curr.Name}"));
            if (currencies.Count > 100)
                admin.TempServerMessage(Localizer.DoStr($"  ... and {currencies.Count - 100} more"));
        }

        [ChatSubCommand("Ea", "Show all currency holdings for an account", "balance", ChatAuthorizationLevel.Admin)]
        public static void Balance(User admin, string accountName = "")
        {
            if (string.IsNullOrWhiteSpace(accountName))
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] Usage: /ea balance <accountName|id>"));
                admin.TempServerMessage(Localizer.DoStr("[EA] Tip: use account ID from /ea accounts, e.g. /ea balance 42"));
                return;
            }

            var account = ResolveAccount(admin, accountName);
            if (account == null) return;

            var holdings = account.CurrencyHoldings
                .Where(h => h.Key != null && h.Value != null && h.Value.Val > 0)
                .OrderByDescending(h => h.Value.Val)
                .ToList();

            if (holdings.Count == 0)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] Account '{account.Name}' has no currency."));
                return;
            }

            admin.TempServerMessage(Localizer.DoStr($"[EA] Account: {account.Name}"));
            admin.TempServerMessage(Localizer.DoStr($"[EA] Holdings ({holdings.Count} currencies):"));
            foreach (var holding in holdings.Take(20))
                admin.TempServerMessage(Localizer.DoStr($"  • {holding.Value.Val:F2} {holding.Key.Name}"));
            if (holdings.Count > 20)
                admin.TempServerMessage(Localizer.DoStr($"  ... and {holdings.Count - 20} more"));
        }

        [ChatSubCommand("Ea", "Add currency to an account.", "add", ChatAuthorizationLevel.Admin)]
        public static void Add(User admin, string accountName = "", string currencyName = "", float amount = 0)
        {
            if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(currencyName) || amount <= 0)
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] Usage: /ea add <accountName|id> <currencyName> <amount>"));
                admin.TempServerMessage(Localizer.DoStr("[EA] Tip: use account ID from /ea accounts, e.g. /ea add 42 Gold 550000"));
                admin.TempServerMessage(Localizer.DoStr("[EA] Tip: use currency ID from /ea currencies, e.g. /ea add 42 7 550000"));
                return;
            }

            var currency = ResolveCurrency(admin, currencyName);
            if (currency == null) return;

            var account = ResolveAccount(admin, accountName);
            if (account == null) return;

            var currentBalance = account.GetCurrencyHoldingVal(currency);
            account.AddCurrency(currency, amount);
            var newBalance = account.GetCurrencyHoldingVal(currency);

            admin.TempServerMessage(Localizer.DoStr($"[EA] Added {amount:F2} {currency.Name}"));
            admin.TempServerMessage(Localizer.DoStr($"[EA] Account: {account.Name} | Before: {currentBalance:F2} → After: {newBalance:F2}"));
        }

        [ChatSubCommand("Ea", "Deduct currency from an account.", "deduct", ChatAuthorizationLevel.Admin)]
        public static void Deduct(User admin, string accountName = "", string currencyName = "", float amount = 0)
        {
            if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(currencyName) || amount <= 0)
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] Usage: /ea deduct <accountName|id> <currencyName> <amount>"));
                admin.TempServerMessage(Localizer.DoStr("[EA] Tip: use account ID from /ea accounts, e.g. /ea deduct 42 Gold 550000"));
                admin.TempServerMessage(Localizer.DoStr("[EA] Tip: use currency ID from /ea currencies, e.g. /ea deduct 42 7 550000"));
                return;
            }

            var currency = ResolveCurrency(admin, currencyName);
            if (currency == null) return;

            var account = ResolveAccount(admin, accountName);
            if (account == null) return;

            var currentBalance = account.GetCurrencyHoldingVal(currency);
            account.AddCurrency(currency, -amount);
            var newBalance = account.GetCurrencyHoldingVal(currency);

            admin.TempServerMessage(Localizer.DoStr($"[EA] Deducted {amount:F2} {currency.Name}"));
            admin.TempServerMessage(Localizer.DoStr($"[EA] Account: {account.Name} | Before: {currentBalance:F2} → After: {newBalance:F2}"));
        }

        [ChatSubCommand("Ea", "Remove ALL of a specific currency from one account", "wipe", ChatAuthorizationLevel.Admin)]
        public static void Wipe(User admin, string accountName = "", string currencyName = "")
        {
            if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(currencyName))
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] Usage: /ea wipe <accountName|id> <currencyName>"));
                admin.TempServerMessage(Localizer.DoStr("[EA] Tip: use account ID from /ea accounts, e.g. /ea wipe 42 Gold"));
                admin.TempServerMessage(Localizer.DoStr("[EA] Tip: use currency ID from /ea currencies, e.g. /ea wipe 42 7"));
                return;
            }

            var currency = ResolveCurrency(admin, currencyName);
            if (currency == null) return;

            var account = ResolveAccount(admin, accountName);
            if (account == null) return;

            var balance = account.GetCurrencyHoldingVal(currency);
            if (balance <= 0)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] Account has no {currency.Name} to remove."));
                return;
            }

            account.AddCurrency(currency, -balance);
            admin.TempServerMessage(Localizer.DoStr($"[EA] Wiped {balance:F2} {currency.Name} from '{account.Name}'"));
        }

        [ChatSubCommand("Ea", "Preview currencies matching a wildcard pattern (* wildcard). Ex: /ea preview *Credit", "preview", ChatAuthorizationLevel.Admin)]
        public static void Preview(User admin, string pattern = "")
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] Usage: /ea preview <pattern>  (use * as wildcard, e.g. *Credit)"));
                return;
            }

            var currencies = GetCurrenciesByPattern(pattern);

            if (currencies.Count == 0)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] No currencies match pattern: '{pattern}'"));
                admin.TempServerMessage(Localizer.DoStr("[EA] Tip: Use * wildcard — examples: *Credit, Test*, *Old*"));
                return;
            }

            admin.TempServerMessage(Localizer.DoStr($"[EA] Pattern '{pattern}' matches {currencies.Count} currencies:"));
            foreach (var curr in currencies.Take(30))
                admin.TempServerMessage(Localizer.DoStr($"  • {curr.Name}"));
            if (currencies.Count > 30)
                admin.TempServerMessage(Localizer.DoStr($"  ... and {currencies.Count - 30} more"));
        }

        [ChatSubCommand("Ea", "DANGER: Remove matching currencies from ALL accounts. Preview first with /ea preview!", "purge", ChatAuthorizationLevel.Admin)]
        public static void Purge(User admin, string pattern = "")
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] Usage: /ea purge <pattern>  (use * as wildcard — run /ea preview first!)"));
                return;
            }

            var currencies = GetCurrenciesByPattern(pattern);

            if (currencies.Count == 0)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] No currencies match pattern: '{pattern}'"));
                return;
            }

            admin.TempServerMessage(Localizer.DoStr($"[EA] ⚠ PURGING {currencies.Count} currencies matching '{pattern}'..."));

            int totalAccountsAffected = 0;
            float totalAmountRemoved = 0f;
            var accounts = BankAccountManager.Obj.Accounts.Where(a => a != null).ToList();

            foreach (var currency in currencies)
            {
                int accountsModified = 0;
                float amountRemoved = 0f;

                foreach (var account in accounts)
                {
                    var balance = account.GetCurrencyHoldingVal(currency);
                    if (balance > 0)
                    {
                        account.AddCurrency(currency, -balance);
                        accountsModified++;
                        amountRemoved += balance;
                    }
                }

                if (accountsModified > 0)
                {
                    totalAccountsAffected += accountsModified;
                    totalAmountRemoved += amountRemoved;
                    admin.TempServerMessage(Localizer.DoStr($"  • {currency.Name}: {amountRemoved:F2} removed from {accountsModified} accounts"));
                }
            }

            if (totalAccountsAffected == 0)
                admin.TempServerMessage(Localizer.DoStr("[EA] No balances found to remove."));
            else
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] ✓ Complete: {currencies.Count} currencies purged"));
                admin.TempServerMessage(Localizer.DoStr($"[EA] Total removed: {totalAmountRemoved:F2} from {totalAccountsAffected} account operations"));
            }
        }

        // ----------------------------
        // ea-gc subcommands
        // ----------------------------

        [ChatSubCommand("EaGc", "Show global currency config and treasury status", "status", ChatAuthorizationLevel.Admin)]
        public static void Status(User admin)
        {
            var cfg = EconAdminPlugin.Config?.Config;
            if (cfg == null)
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] EconAdmin config not loaded."));
                return;
            }

            if (string.IsNullOrEmpty(cfg.GlobalCurrencyName))
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] No global currency configured. Set GlobalCurrencyName in EconAdmin.eco config."));
                return;
            }

            var currency = CurrencyManager.Currencies
                .FirstOrDefault(c => c != null && c.Name.Equals(cfg.GlobalCurrencyName, StringComparison.OrdinalIgnoreCase));

            string currencyStatus = currency != null ? "Found" : "Not created yet — run /ea-gc create";
            string treasuryName = EconAdminPlugin.ResolveTreasuryName(cfg);

            var treasury = BankAccountManager.Obj.Accounts
                .FirstOrDefault(a => a != null && a.Name != null &&
                                     a.Name.Equals(treasuryName, StringComparison.OrdinalIgnoreCase));

            string treasuryBalance = (treasury != null && currency != null)
                ? treasury.GetCurrencyHoldingVal(currency).ToString("F2") : "N/A";
            string treasuryStatus = treasury != null
                ? $"Found | Balance: {treasuryBalance}"
                : "Not created yet — run /ea-gc create";

            admin.TempServerMessage(Localizer.DoStr("[EA] === Global Currency Status ==="));
            admin.TempServerMessage(Localizer.DoStr($"[EA] Currency Name:   {cfg.GlobalCurrencyName}"));
            admin.TempServerMessage(Localizer.DoStr($"[EA] Currency:        {currencyStatus}"));
            admin.TempServerMessage(Localizer.DoStr($"[EA] Treasury:        {treasuryStatus}"));
            admin.TempServerMessage(Localizer.DoStr($"[EA] New Player Gift: {(cfg.NewPlayerGiftAmount > 0 ? cfg.NewPlayerGiftAmount.ToString("N0") : "Disabled")}"));
        }

        [ChatSubCommand("EaGc", "Create the global currency and treasury account defined in config", "create", ChatAuthorizationLevel.Admin)]
        public static void Create(User admin)
        {
            var cfg = EconAdminPlugin.Config?.Config;
            if (cfg == null)
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] EconAdmin config not loaded."));
                return;
            }

            if (string.IsNullOrEmpty(cfg.GlobalCurrencyName))
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] GlobalCurrencyName is not set in your EconAdmin.eco config file."));
                return;
            }

            var currency = CurrencyManager.Currencies
                .FirstOrDefault(c => c != null && c.Name.Equals(cfg.GlobalCurrencyName, StringComparison.OrdinalIgnoreCase));

            if (currency == null)
            {
                currency = CurrencyManager.AddCurrency(admin, cfg.GlobalCurrencyName, CurrencyType.Backed);
                admin.TempServerMessage(Localizer.DoStr($"[EA] Created currency: {cfg.GlobalCurrencyName}"));
            }
            else
                admin.TempServerMessage(Localizer.DoStr($"[EA] Currency '{cfg.GlobalCurrencyName}' already exists."));

            if (currency != null && currency.BackingItem == null)
            {
                currency.BackingItem = new ClaimPaperItem();
                currency.SaveInRegistrar();
            }

            string treasuryName = EconAdminPlugin.ResolveTreasuryName(cfg);
            var treasury = BankAccountManager.Obj.Accounts
                .FirstOrDefault(a => a != null && a.Name != null &&
                                     a.Name.Equals(treasuryName, StringComparison.OrdinalIgnoreCase));

            if (treasury == null)
            {
                BankAccountManager.CreateAccount(admin, treasuryName);
                treasury = BankAccountManager.Obj.Accounts
                    .FirstOrDefault(a => a != null && a.Name != null &&
                                         a.Name.Equals(treasuryName, StringComparison.OrdinalIgnoreCase));

                if (treasury != null && currency != null)
                {
                    BankAccountManager.AddAccountManager(admin, treasury, admin);
                    treasury.AddCurrency(currency, (float)cfg.TreasuryInitialBalance);
                    admin.TempServerMessage(Localizer.DoStr($"[EA] Created treasury '{treasuryName}' with {cfg.TreasuryInitialBalance:N0} {cfg.GlobalCurrencyName}"));
                }
            }
            else
                admin.TempServerMessage(Localizer.DoStr($"[EA] Treasury '{treasuryName}' already exists."));

            admin.TempServerMessage(Localizer.DoStr("[EA] Done. Use /ea-gc status to verify."));
        }

        [ChatSubCommand("EaGc", "Gift global currency to an account. Defaults to configured gift amount if no amount given.", "gift", ChatAuthorizationLevel.Admin)]
        public static void Gift(User admin, string accountName = "", int amount = 0)
        {
            if (string.IsNullOrWhiteSpace(accountName))
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] Usage: /eagc gift <accountName|id> [amount]"));
                admin.TempServerMessage(Localizer.DoStr("[EA] Tip: use account ID from /ea accounts, e.g. /eagc gift 42"));
                return;
            }

            var cfg = EconAdminPlugin.Config?.Config;
            if (cfg == null || string.IsNullOrEmpty(cfg.GlobalCurrencyName))
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] Global currency is not configured."));
                return;
            }

            var currency = CurrencyManager.Currencies
                .FirstOrDefault(c => c != null && c.Name.Equals(cfg.GlobalCurrencyName, StringComparison.OrdinalIgnoreCase));

            if (currency == null)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] Global currency '{cfg.GlobalCurrencyName}' not found. Run /ea-gc create first."));
                return;
            }

            var account = ResolveAccount(admin, accountName);
            if (account == null) return;

            int giftAmount = amount > 0 ? amount : cfg.NewPlayerGiftAmount;
            if (giftAmount <= 0)
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] No amount given and NewPlayerGiftAmount is 0. Provide an explicit amount."));
                return;
            }

            account.AddCurrency(currency, (float)giftAmount);
            admin.TempServerMessage(Localizer.DoStr($"[EA] Gifted {giftAmount:N0} {cfg.GlobalCurrencyName} to '{account.Name}'"));
        }

        [ChatSubCommand("EaGc", "Mint additional global currency directly into the treasury account", "mint", ChatAuthorizationLevel.Admin)]
        public static void Mint(User admin, int amount = 0)
        {
            if (amount <= 0)
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] Usage: /eagc mint <amount>  (must be greater than 0)"));
                return;
            }

            var cfg = EconAdminPlugin.Config?.Config;
            if (cfg == null || string.IsNullOrEmpty(cfg.GlobalCurrencyName))
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] Global currency is not configured."));
                return;
            }

            var currency = CurrencyManager.Currencies
                .FirstOrDefault(c => c != null && c.Name.Equals(cfg.GlobalCurrencyName, StringComparison.OrdinalIgnoreCase));

            if (currency == null)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] Global currency '{cfg.GlobalCurrencyName}' not found. Run /ea-gc create first."));
                return;
            }

            string treasuryName = EconAdminPlugin.ResolveTreasuryName(cfg);
            var treasury = BankAccountManager.Obj.Accounts
                .FirstOrDefault(a => a != null && a.Name != null &&
                                     a.Name.Equals(treasuryName, StringComparison.OrdinalIgnoreCase));

            if (treasury == null)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] Treasury '{treasuryName}' not found. Run /ea-gc create first."));
                return;
            }

            var before = treasury.GetCurrencyHoldingVal(currency);
            treasury.AddCurrency(currency, (float)amount);
            var after = treasury.GetCurrencyHoldingVal(currency);

            admin.TempServerMessage(Localizer.DoStr($"[EA] Minted {amount:N0} {cfg.GlobalCurrencyName} into '{treasuryName}'"));
            admin.TempServerMessage(Localizer.DoStr($"[EA] Treasury balance: {before:F2} → {after:F2}"));
        }
    }
}
