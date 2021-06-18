﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechShopSolution.ViewModels.Catalog.Product;
using TechShopSolution.ViewModels.Common;

namespace TechShopSolution.WebApp.Models
{
    public class HomeViewModel
    {
        public List<ProductViewModel> FeaturedProducts { get; set; }
        public List<ProductViewModel> BestSellerProducts { get; set; }
        public List<ProductViewModel> GetProductsByCategoryy { get; set; }

    }
}
