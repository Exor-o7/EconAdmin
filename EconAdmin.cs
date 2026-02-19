// EconAdmin
// A comprehensive admin toolkit for managing currencies and bank accounts on ECO servers
// Author: Exor
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

        [LocDescription("Starting balance deposited into the treasury when it is first created via /ea-gc-setup.")]
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
    public class EconAdminCommands
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

        /// <summary>Check if a currency name matches a wildcard pattern (* wildcard)</summary>
        private static bool MatchesWildcard(string currencyName, string pattern)
        {
            int starCount = pattern.Count(c => c == '*');
            
            if (starCount == 0)
                return currencyName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
            
            if (starCount == 1)
            {
                int starPos = pattern.IndexOf('*');
                
                // *suffix (matches anything ending with suffix)
                if (starPos == 0)
                {
                    string suffix = pattern.Substring(1).TrimStart();
                    if (currencyName.EndsWith(" " + suffix, StringComparison.OrdinalIgnoreCase))
                        return true;
                    return currencyName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
                }
                
                // prefix* (matches anything starting with prefix)
                if (starPos == pattern.Length - 1)
                {
                    string prefix = pattern.Substring(0, pattern.Length - 1).TrimEnd();
                    if (currencyName.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
                        return true;
                    return currencyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
                }
                
                // prefix*suffix (matches prefix + anything + suffix)
                string prefixPart = pattern.Substring(0, starPos);
                string suffixPart = pattern.Substring(starPos + 1);
                return currencyName.StartsWith(prefixPart, StringComparison.OrdinalIgnoreCase) &&
                       currencyName.EndsWith(suffixPart, StringComparison.OrdinalIgnoreCase) &&
                       currencyName.Length >= prefixPart.Length + suffixPart.Length;
            }
            
            if (starCount == 2 && pattern.StartsWith("*") && pattern.EndsWith("*"))
            {
                // *contains* (matches anything containing text)
                string contains = pattern.Substring(1, pattern.Length - 2).Trim();
                return currencyName.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            
            // Fallback for complex patterns
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
        // Commands
        // ----------------------------

        [ChatCommand("List all bank accounts (optional: search filter)", "ea-accounts", ChatAuthorizationLevel.Admin)]
        public static void ListAccounts(User admin, string search = "")
        {
            search = search ?? string.Empty;
            
            var accounts = BankAccountManager.Obj.Accounts
                .Where(a => a != null && a.Name != null &&
                            (string.IsNullOrEmpty(search) || 
                             a.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                .Take(100)
                .Select(a => a.Name)
                .ToList();

            if (accounts.Count == 0)
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] No matching accounts found."));
                return;
            }

            admin.TempServerMessage(Localizer.DoStr($"[EA] Found {accounts.Count} account(s):"));
            foreach (var accountName in accounts)
            {
                admin.TempServerMessage(Localizer.DoStr($"  • {accountName}"));
            }
        }

        [ChatCommand("List all currencies in the system", "ea-currencies", ChatAuthorizationLevel.Admin)]
        public static void ListAllCurrencies(User admin, string filter = "")
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
            {
                admin.TempServerMessage(Localizer.DoStr($"  • {curr.Name}"));
            }
            if (currencies.Count > 100)
                admin.TempServerMessage(Localizer.DoStr($"  ... and {currencies.Count - 100} more"));
        }

        [ChatCommand("Add/remove currency amount to/from an account. Use negative to remove (ex: /ea-adjust PlayerName CurrencyName -500)", "ea-adjust", ChatAuthorizationLevel.Admin)]
        public static void ModifyAccountCurrency(User admin, string accountName, string currencyName, float amount)
        {
            var currency = GetCurrencyByName(currencyName);
            if (currency == null)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] Currency '{currencyName}' not found."));
                admin.TempServerMessage(Localizer.DoStr("[EA] Use /ea-currencies to see all currencies."));
                return;
            }

            var account = BankAccountManager.Obj.Accounts
                .FirstOrDefault(a => a != null && a.Name != null &&
                                     a.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase));

            if (account == null)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] Account '{accountName}' not found."));
                admin.TempServerMessage(Localizer.DoStr("[EA] Use /ea-accounts to search for accounts."));
                return;
            }

            var currentBalance = account.GetCurrencyHoldingVal(currency);
            account.AddCurrency(currency, amount);
            var newBalance = account.GetCurrencyHoldingVal(currency);

            var action = amount >= 0 ? "Added" : "Removed";
            admin.TempServerMessage(Localizer.DoStr($"[EA] {action} {Math.Abs(amount):F2} {currency.Name}"));
            admin.TempServerMessage(Localizer.DoStr($"[EA] Account: {account.Name} | Before: {currentBalance:F2} → After: {newBalance:F2}"));
        }

        [ChatCommand("Remove ALL of a specific currency from one account", "ea-wipe", ChatAuthorizationLevel.Admin)]
        public static void WipeCurrencyFromAccount(User admin, string accountName, string currencyName)
        {
            var currency = GetCurrencyByName(currencyName);
            if (currency == null)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] Currency '{currencyName}' not found."));
                return;
            }

            var account = BankAccountManager.Obj.Accounts
                .FirstOrDefault(a => a != null && a.Name != null &&
                                     a.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase));

            if (account == null)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] Account '{accountName}' not found."));
                return;
            }

            var balance = account.GetCurrencyHoldingVal(currency);
            if (balance <= 0)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] Account has no {currency.Name} to remove."));
                return;
            }

            account.AddCurrency(currency, -balance);
            admin.TempServerMessage(Localizer.DoStr($"[EA] Wiped {balance:F2} {currency.Name} from '{account.Name}'"));
        }

        [ChatCommand("Preview currencies that match pattern. Supports wildcards: *Credit, Player*, *Old* (ex: /ea-preview *Credit)", "ea-preview", ChatAuthorizationLevel.Admin)]
        public static void PreviewCurrencyPattern(User admin, string pattern)
        {
            var currencies = GetCurrenciesByPattern(pattern);
            
            if (currencies.Count == 0)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] No currencies match pattern: '{pattern}'"));
                admin.TempServerMessage(Localizer.DoStr("[EA] Tip: Use * wildcard - examples: *Credit, Test*, *Old*"));
                return;
            }

            admin.TempServerMessage(Localizer.DoStr($"[EA] Pattern '{pattern}' matches {currencies.Count} currencies:"));
            foreach (var curr in currencies.Take(30))
            {
                admin.TempServerMessage(Localizer.DoStr($"  • {curr.Name}"));
            }
            if (currencies.Count > 30)
                admin.TempServerMessage(Localizer.DoStr($"  ... and {currencies.Count - 30} more"));
        }

        [ChatCommand("DANGER: Remove matching currencies from ALL accounts. Preview with /ea-preview first! (ex: /ea-purge *Credit)", "ea-purge", ChatAuthorizationLevel.Admin)]
        public static void PurgeCurrencies(User admin, string pattern)
        {
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
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] No balances found to remove."));
            }
            else
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] ✓ Complete: {currencies.Count} currencies purged"));
                admin.TempServerMessage(Localizer.DoStr($"[EA] Total removed: {totalAmountRemoved:F2} from {totalAccountsAffected} account operations"));
            }
        }

        [ChatCommand("Get detailed balance info for an account", "ea-balance", ChatAuthorizationLevel.Admin)]
        public static void GetAccountBalance(User admin, string accountName)
        {
            var account = BankAccountManager.Obj.Accounts
                .FirstOrDefault(a => a != null && a.Name != null &&
                                     a.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase));

            if (account == null)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] Account '{accountName}' not found."));
                return;
            }

            var holdings = account.CurrencyHoldings
                .Where(h => h != null && h.Currency != null && h.Val > 0)
                .OrderByDescending(h => h.Val)
                .ToList();

            if (holdings.Count == 0)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] Account '{account.Name}' has no currency."));
                return;
            }

            admin.TempServerMessage(Localizer.DoStr($"[EA] Account: {account.Name}"));
            admin.TempServerMessage(Localizer.DoStr($"[EA] Holdings ({holdings.Count} currencies):"));
            foreach (var holding in holdings.Take(20))
            {
                admin.TempServerMessage(Localizer.DoStr($"  • {holding.Val:F2} {holding.Currency.Name}"));
            }
            if (holdings.Count > 20)
                admin.TempServerMessage(Localizer.DoStr($"  ... and {holdings.Count - 20} more"));
        }

        // ----------------------------
        // Global Currency Commands
        // ----------------------------

        [ChatCommand("Show global currency config and treasury status", "ea-gc-status", ChatAuthorizationLevel.Admin)]
        public static void GlobalCurrencyStatus(User admin)
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

            string currencyStatus = currency != null ? "Found" : "Not created yet — run /ea-gc-setup";
            string treasuryName = cfg.GlobalCurrencyName + " - Treasury";

            var treasury = BankAccountManager.Obj.Accounts
                .FirstOrDefault(a => a != null && a.Name != null &&
                                     a.Name.Equals(treasuryName, StringComparison.OrdinalIgnoreCase));

            string treasuryBalance = (treasury != null && currency != null)
                ? treasury.GetCurrencyHoldingVal(currency).ToString("F2")
                : "N/A";
            string treasuryStatus = treasury != null
                ? $"Found | Balance: {treasuryBalance}"
                : "Not created yet — run /ea-gc-setup";

            admin.TempServerMessage(Localizer.DoStr($"[EA] === Global Currency Status ==="));
            admin.TempServerMessage(Localizer.DoStr($"[EA] Currency Name:   {cfg.GlobalCurrencyName}"));
            admin.TempServerMessage(Localizer.DoStr($"[EA] Currency:        {currencyStatus}"));
            admin.TempServerMessage(Localizer.DoStr($"[EA] Treasury:        {treasuryStatus}"));
            admin.TempServerMessage(Localizer.DoStr($"[EA] New Player Gift: {(cfg.NewPlayerGiftAmount > 0 ? cfg.NewPlayerGiftAmount.ToString("N0") : "Disabled")}"));
        }

        [ChatCommand("Create the global currency and treasury account defined in config", "ea-gc-setup", ChatAuthorizationLevel.Admin)]
        public static void SetupGlobalCurrency(User admin)
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

            // Create currency if missing
            var currency = CurrencyManager.Currencies
                .FirstOrDefault(c => c != null && c.Name.Equals(cfg.GlobalCurrencyName, StringComparison.OrdinalIgnoreCase));

            if (currency == null)
            {
                currency = CurrencyManager.AddCurrency(admin, cfg.GlobalCurrencyName, CurrencyType.Backed);
                admin.TempServerMessage(Localizer.DoStr($"[EA] Created currency: {cfg.GlobalCurrencyName}"));
            }
            else
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] Currency '{cfg.GlobalCurrencyName}' already exists."));
            }

            if (currency != null && currency.BackingItem == null)
            {
                currency.BackingItem = new ClaimPaperItem();
                currency.SaveInRegistrar();
            }

            // Create treasury account if missing
            string treasuryName = cfg.GlobalCurrencyName + " - Treasury";
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
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] Treasury '{treasuryName}' already exists."));
            }

            admin.TempServerMessage(Localizer.DoStr("[EA] Setup complete. Use /ea-gc-status to verify."));
        }

        [ChatCommand("Manually gift global currency to an account. Defaults to configured gift amount if no amount given.", "ea-gc-gift", ChatAuthorizationLevel.Admin)]
        public static void GiftGlobalCurrency(User admin, string accountName, int amount = 0)
        {
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
                admin.TempServerMessage(Localizer.DoStr($"[EA] Global currency '{cfg.GlobalCurrencyName}' not found. Run /ea-gc-setup first."));
                return;
            }

            var account = BankAccountManager.Obj.Accounts
                .FirstOrDefault(a => a != null && a.Name != null &&
                                     a.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase));

            if (account == null)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] Account '{accountName}' not found. Use /ea-accounts to search."));
                return;
            }

            int giftAmount = amount > 0 ? amount : cfg.NewPlayerGiftAmount;
            if (giftAmount <= 0)
            {
                admin.TempServerMessage(Localizer.DoStr("[EA] No amount given and NewPlayerGiftAmount is 0. Provide an explicit amount."));
                return;
            }

            account.AddCurrency(currency, (float)giftAmount);
            admin.TempServerMessage(Localizer.DoStr($"[EA] Gifted {giftAmount:N0} {cfg.GlobalCurrencyName} to '{account.Name}'"));
        }

        [ChatCommand("Mint additional global currency directly into the treasury account", "ea-gc-mint", ChatAuthorizationLevel.Admin)]
        public static void MintToTreasury(User admin, int amount)
        {
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
                admin.TempServerMessage(Localizer.DoStr($"[EA] Global currency '{cfg.GlobalCurrencyName}' not found. Run /ea-gc-setup first."));
                return;
            }

            string treasuryName = cfg.GlobalCurrencyName + " - Treasury";
            var treasury = BankAccountManager.Obj.Accounts
                .FirstOrDefault(a => a != null && a.Name != null &&
                                     a.Name.Equals(treasuryName, StringComparison.OrdinalIgnoreCase));

            if (treasury == null)
            {
                admin.TempServerMessage(Localizer.DoStr($"[EA] Treasury '{treasuryName}' not found. Run /ea-gc-setup first."));
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
