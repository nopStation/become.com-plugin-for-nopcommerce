using Nop.Core;
using Nop.Services.Plugins;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using System.Threading.Tasks;

namespace Nop.Plugin.Feed.Become
{
    public class BecomeService : BasePlugin,  IMiscPlugin
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;


        #endregion

        #region Ctor

        public BecomeService(ILocalizationService localizationService,
            ISettingService settingService,
            IWebHelper webHelper)
        {
            _settingService = settingService;
            _webHelper = webHelper;
            _localizationService = localizationService;
        }

        #endregion

        #region Methods

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/FeedBecome/Configure";
        }

        /// <summary>
        /// Install plugin
        /// </summary>
        public override async Task InstallAsync()
        {
            //settings
            var settings = new BecomeSettings()
            {
                ProductPictureSize = 125
            };

            await _settingService.SaveSettingAsync(settings);

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Feed.Become.ClickHere", "Click here");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Feed.Become.Currency", "Currency");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Feed.Become.Currency.Hint", "Select the default currency that will be used to generate the feed.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Feed.Become.Generate", "Generate feed");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Feed.Become.ProductPictureSize", "Product thumbnail image size");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Feed.Become.ProductPictureSize.Hint", "The default size (pixels) for product thumbnail images.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Feed.Become.SuccessResult", "Become.com feed has been successfully generated. {0} to see generated feed");

            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            //settings
            await _settingService.DeleteSettingAsync<BecomeSettings>();

            //locales
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Feed.Become.ClickHere");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Feed.Become.Currency");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Feed.Become.Currency.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Feed.Become.Generate");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Feed.Become.ProductPictureSize");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Feed.Become.ProductPictureSize.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Feed.Become.SuccessResult");

            await base.UninstallAsync();
        }

        #endregion
    }
}
