using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Stores;
using Nop.Plugin.Feed.Become.Models;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Html;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Services.Seo;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Feed.Become.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    public class FeedBecomeController : BasePluginController
    {
        #region Fields

        private readonly BecomeSettings _becomeSettings;
        private readonly ICurrencyService _currencyService;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;
        private readonly INotificationService _notificationService;
        private readonly CurrencySettings _currencySettings;
        private readonly ICategoryService _categoryService;
        private readonly IManufacturerService _manufacturerService;
        private readonly IPictureService _pictureService;
        private readonly IProductService _productService;
        private readonly IUrlRecordService _urlRecordService;
        private readonly IHtmlFormatter _htmlFormatter;

        #endregion

        #region Ctor

        public FeedBecomeController(BecomeSettings becomeSettings,
            ICurrencyService currencyService,
            IWebHostEnvironment hostingEnvironment,
            ILocalizationService localizationService,
            ILogger logger,
            IPermissionService permissionService,
            ISettingService settingService,
            IStoreContext storeContext,
            IWebHelper webHelper,
            INotificationService notificationService,
            CurrencySettings currencySettings,
            ICategoryService categoryService,
            IManufacturerService manufacturerService,
            IProductService productService,
            IPictureService pictureService,
            IUrlRecordService urlRecordService, IHtmlFormatter htmlFormatter)
        {
            _becomeSettings = becomeSettings;
            _currencyService = currencyService;
            _hostingEnvironment = hostingEnvironment;
            _localizationService = localizationService;
            _logger = logger;
            _permissionService = permissionService;
            _settingService = settingService;
            _storeContext = storeContext;
            _webHelper = webHelper;
            _notificationService = notificationService;
            _currencySettings = currencySettings;
            _categoryService = categoryService;
            _manufacturerService = manufacturerService;
            _productService = productService;
            _pictureService = pictureService;
            _urlRecordService = urlRecordService;
            _htmlFormatter = htmlFormatter;
        }

        #endregion

        #region Utilities

        private static string RemoveSpecChars(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            s = s.Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ');

            return s;
        }

        private async Task<IList<Category>> GetCategoryBreadCrumbAsync(Category category)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));

            var breadCrumb = new List<Category>();

            while (category != null //category is not null
                && !category.Deleted //category is not deleted
                && category.Published) //category is published
            {
                breadCrumb.Add(category);

                category = await _categoryService.GetCategoryByIdAsync(category.ParentCategoryId);
            }

            breadCrumb.Reverse();

            return breadCrumb;
        }

        private async Task<Currency> GetUsedCurrencyAsync()
        {
            var currency = await _currencyService.GetCurrencyByIdAsync(_becomeSettings.CurrencyId);

            if (currency == null || !currency.Published)
                currency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);

            return currency;
        }

        /// <summary>
        /// Generate a feed
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="store">Store</param>
        /// <returns>Generated feed</returns>
        public async Task GenerateFeedAsync(Stream stream, Store store)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (store == null)
                throw new ArgumentNullException(nameof(store));

            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine("UPC;Mfr Part #;Manufacturer;Product URL;Image URL;Product Title;Product Description;Category;Price;Condition;Stock Status");

                var products1 = await _productService.SearchProductsAsync(storeId: store.Id, visibleIndividuallyOnly: true);

                foreach (var product1 in products1)
                {
                    var productsToProcess = new List<Product>();

                    switch (product1.ProductType)
                    {
                        case ProductType.SimpleProduct:
                            {
                                //simple product doesn't have child products
                                productsToProcess.Add(product1);
                            }
                            break;
                        case ProductType.GroupedProduct:
                            {
                                //grouped products could have several child products

                                var associatedProducts = await _productService.GetAssociatedProductsAsync(product1.Id, store.Id);

                                productsToProcess.AddRange(associatedProducts);
                            }
                            break;
                        default:
                            continue;
                    }
                    foreach (var product in productsToProcess)
                    {

                        var sku = product.Id.ToString("000000000000");
                        var productManufacturers = await _manufacturerService.GetProductManufacturersByProductIdAsync(product.Id);
                        var manufacturerPartNumber = "";
                        var manufacturerName = "";

                        if (productManufacturers.Count > 0)
                        {
                            var manufacturer = await _manufacturerService.GetManufacturerByIdAsync(productManufacturers[0].ManufacturerId);
                            manufacturerName = manufacturer.Name;
                            manufacturerPartNumber = product.ManufacturerPartNumber;
                        }

                        var productTitle = product.Name;
                        //TODO add a method for getting product URL (e.g. SEOHelper.GetProductUrl)
                        var productUrl = $"{_webHelper.GetStoreLocation(false)}{await _urlRecordService.GetSeNameAsync(product)}";

                        var pictures = await _pictureService.GetPicturesByProductIdAsync(product.Id, 1);

                        //always use HTTP when getting image URL
                        var imageUrl = pictures.Count > 0
                            ? (await _pictureService.GetPictureUrlAsync(pictures[0], _becomeSettings.ProductPictureSize, storeLocation: store.Url)).Url
                            : await _pictureService.GetDefaultPictureUrlAsync(_becomeSettings.ProductPictureSize, storeLocation: store.Url);

                        var description = product.FullDescription;
                        var currency = await GetUsedCurrencyAsync();
                        var price = (await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(product.Price, currency)).ToString(new CultureInfo("en-US", false).NumberFormat);
                        var stockStatus = product.StockQuantity > 0 ? "In Stock" : "Out of Stock";
                        var category = "no category";

                        if (string.IsNullOrEmpty(description))
                        {
                            description = product.ShortDescription;
                        }

                        if (string.IsNullOrEmpty(description))
                        {
                            description = product.Name;
                        }

                        var productCategories = await _categoryService.GetProductCategoriesByProductIdAsync(product.Id);

                        if (productCategories.Count > 0)
                        {
                            var firstCategory = await _categoryService.GetCategoryByIdAsync(productCategories[0].CategoryId);

                            if (firstCategory != null)
                            {
                                var sb = new StringBuilder();

                                foreach (var cat in await GetCategoryBreadCrumbAsync(firstCategory))
                                {
                                    sb.AppendFormat("{0}>", cat.Name);
                                }

                                sb.Length -= 1;
                                category = sb.ToString();
                            }
                        }

                        productTitle = CommonHelper.EnsureMaximumLength(productTitle, 80);
                        productTitle = RemoveSpecChars(productTitle);

                        manufacturerPartNumber = RemoveSpecChars(manufacturerPartNumber);
                        manufacturerName = RemoveSpecChars(manufacturerName);

                        description = _htmlFormatter.StripTags(description);
                        description = CommonHelper.EnsureMaximumLength(description, 250);
                        description = RemoveSpecChars(description);

                        category = RemoveSpecChars(category);

                        writer.WriteLine("{0};{1};{2};{3};{4};{5};{6};{7};{8};New;{9}",
                            sku,
                            manufacturerPartNumber,
                            manufacturerName,
                            productUrl,
                            imageUrl,
                            productTitle,
                            description,
                            category,
                            price,
                            stockStatus);
                    }
                }
            }
        }

        #endregion

        #region Methods

        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageProducts))
                return AccessDeniedView();

            var model = new FeedBecomeModel
            {
                ProductPictureSize = _becomeSettings.ProductPictureSize,
                CurrencyId = _becomeSettings.CurrencyId
            };

            foreach (var c in await _currencyService.GetAllCurrenciesAsync())
            {
                model.AvailableCurrencies.Add(new SelectListItem()
                    {
                         Text = c.Name,
                         Value = c.Id.ToString()
                    });
            }

            return View("~/Plugins/Feed.Become/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [FormValueRequired("save")]
        public async Task<IActionResult> Configure(FeedBecomeModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageProducts))
                return AccessDeniedView();

            //save settings
            _becomeSettings.ProductPictureSize = model.ProductPictureSize;
            _becomeSettings.CurrencyId = model.CurrencyId;

            await _settingService.SaveSettingAsync(_becomeSettings);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            //redisplay the form
            foreach (var c in await _currencyService.GetAllCurrenciesAsync())
            {
                model.AvailableCurrencies.Add(new SelectListItem
                {
                    Text = c.Name,
                    Value = c.Id.ToString()
                });
            }

            return View("~/Plugins/Feed.Become/Views/Configure.cshtml", model);
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("generate")]
        public async Task<IActionResult> GenerateFeed(FeedBecomeModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageProducts))
                return AccessDeniedView();

            try
            {
                var fileName = $"become_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}_{CommonHelper.GenerateRandomDigitCode(4)}.csv";
                var filePath = Path.Combine(_hostingEnvironment.WebRootPath, "files\\exportimport", fileName);

                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    await GenerateFeedAsync(fs, _storeContext.GetCurrentStore());
                }

                var clickhereStr = $"<a href=\"{_webHelper.GetStoreLocation(false)}/files/exportimport/{fileName}\" target=\"_blank\">{await _localizationService.GetResourceAsync("Plugins.Feed.Become.ClickHere")}</a>";
                var result = string.Format(await _localizationService.GetResourceAsync("Plugins.Feed.Become.SuccessResult"), clickhereStr);

                model.GenerateFeedResult = result;
            }
            catch (Exception exc)
            {
                model.GenerateFeedResult = exc.Message;
                await _logger.ErrorAsync(exc.Message, exc);
            }

            foreach (var c in await _currencyService.GetAllCurrenciesAsync(false))
            {
                model.AvailableCurrencies.Add(new SelectListItem()
                {
                    Text = c.Name,
                    Value = c.Id.ToString()
                });
            }

            return View("~/Plugins/Feed.Become/Views/Configure.cshtml", model);
        }

        #endregion
    }
}
