using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Feed.Become.Models
{
    public class FeedBecomeModel
    {
        public FeedBecomeModel()
        {
            AvailableCurrencies = new List<SelectListItem>();
        }

        [NopResourceDisplayName("Plugins.Feed.Become.ProductPictureSize")]
        public int ProductPictureSize { get; set; }

        [NopResourceDisplayName("Plugins.Feed.Become.Currency")]
        public int CurrencyId { get; set; }

        public IList<SelectListItem> AvailableCurrencies { get; set; }

        public string GenerateFeedResult { get; set; }
    }
}