﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TechShopSolution.ViewModels.Catalog.Product;
using Microsoft.AspNetCore.Hosting;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.IO;
using TechShopSolution.ApiIntegration;
using TechShopSolution.ViewModels.Catalog.Category;
using System;
using System.Linq;

namespace TechShopSolution.AdminApp.Controllers
{
    [Authorize]
    public class ProductController : Controller
    {
        private readonly IProductApiClient _productApiClient;
        [Obsolete]
        private readonly IHostingEnvironment _environment;

        [Obsolete]
        public ProductController(IProductApiClient productApiClient, IHostingEnvironment environment)
        {
            _productApiClient = productApiClient;
            _environment = environment;
        }
        public async Task<IActionResult> Index(string keyword, int? CategoryID, int? BrandID, int pageIndex = 1, int pageSize = 10)
        {
            var categoryList = await _productApiClient.GetAllCategory();
            List<int?> lstIDCate = await findChildCategory(categoryList, CategoryID);
            var request = new GetProductPagingRequest()
            {
                Keyword = keyword,
                BrandID = BrandID,
                CategoryID = lstIDCate,
                PageIndex = pageIndex,
                PageSize = pageSize,
            };
            var data = await _productApiClient.GetProductPagings(request);
            ViewBag.Keyword = keyword;
           
            if (TempData["result"] != null)
            {
                ViewBag.SuccessMsg = TempData["result"];
            }
            ViewBag.ListCate = await OrderCateToTree(categoryList);
            ViewBag.ListBrand = await _productApiClient.GetAllBrand();
            return View(data);
        }
        public async Task<List<CategoryViewModel>> OrderCateToTree(List<CategoryViewModel> lst, int parent_id = 0, int level = 0)
        {
            if (lst != null)
            {
                List<CategoryViewModel> result = new List<CategoryViewModel>();
                foreach (CategoryViewModel cate in lst)
                {
                    if (cate.parent_id == parent_id)
                    {
                        CategoryViewModel tree = new CategoryViewModel();
                        tree = cate;
                        tree.level = level;
                        tree.cate_name = String.Concat(Enumerable.Repeat("|————", level)) + tree.cate_name;

                        result.Add(tree);
                        List<CategoryViewModel> child = await OrderCateToTree(lst, cate.id, level + 1);
                        result.AddRange(child);
                    }
                }
                return result;
            }
            return null;
        }
        public async Task<List<int?>> findChildCategory(List<CategoryViewModel> lst, int? categoryID)
        {
            List<int?> CateIDs = new List<int?>();
            if (categoryID != null)
            {
                CateIDs.Add(categoryID);
                List<CategoryViewModel> lstCateChild = new List<CategoryViewModel>();
                lstCateChild = await OrderCateToTree(lst, (int)categoryID);
                foreach(var cate in lstCateChild)
                {
                    CateIDs.Add(cate.id);
                }
            }
            return CateIDs;
        }
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var categoryList = await _productApiClient.GetAllCategory();
            ViewBag.ListCate = await OrderCateToTree(categoryList);
            ViewBag.ListBrand = await _productApiClient.GetAllBrand();
            return View();
        }
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] ProductCreateRequest request)
        {
            if (!ModelState.IsValid)
                return View();
            var result = await _productApiClient.CreateProduct(request);
            if (result.IsSuccess)
            {
                TempData["result"] = "Thêm sản phẩm thành công";
                return RedirectToAction("Index");
            }
            ModelState.AddModelError("", result.Message);
            return View(request);
        }
        [HttpGet]
        public async Task<IActionResult> Update(int id)
        {
            var imageList = await Task.Run(() => _productApiClient.GetImageByProductID(id));
            ViewData["imageList"] = imageList;
            var result = await Task.Run(() => _productApiClient.GetById(id));
            if (!result.IsSuccess || result.ResultObject == null)
            {
                ModelState.AddModelError("", result.Message);
                return View("Index");
            }
            var updateRequest = new ProductUpdateRequest()
            {
                Id = result.ResultObject.id,
                Best_seller = result.ResultObject.best_seller,
                Brand_id = result.ResultObject.brand_id,
                CateID = result.ResultObject.CateID,
                Code = result.ResultObject.code,
                Descriptions = result.ResultObject.descriptions,
                Featured = result.ResultObject.featured,
                Instock = result.ResultObject.instock,
                IsActive = result.ResultObject.isActive,
                Meta_descriptions = result.ResultObject.meta_descriptions,
                Meta_keywords = result.ResultObject.meta_keywords,
                Meta_tittle = result.ResultObject.meta_tittle,
                Name = result.ResultObject.name,
                Promotion_price = result.ResultObject.promotion_price,
                Short_desc = result.ResultObject.short_desc,
                Slug = result.ResultObject.slug,
                Specifications = result.ResultObject.specifications,
                Unit_price = result.ResultObject.unit_price,
                Warranty = result.ResultObject.warranty
            };
            if (TempData["result"] != null)
            {
                ViewBag.SuccessMsg = TempData["result"];
            }
            var categoryList = await _productApiClient.GetAllCategory();
            ViewBag.ListCate = await OrderCateToTree(categoryList);
            ViewBag.ListBrand = await _productApiClient.GetAllBrand();
            return View(updateRequest);
        }
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update(ProductUpdateRequest request)
        {
            if (!ModelState.IsValid)
                return RedirectToAction("Update",request.Id);
            var result = await _productApiClient.UpdateProduct(request);
            if (result.IsSuccess)
            {
                TempData["result"] = "Cập nhật sản phẩm thành công";
                return RedirectToAction("Index");
            }
            ModelState.AddModelError("", result.Message);
            return View(request);
        }
        public async Task sendListMoreImage(List<IFormFile> files)
        {
            if (files != null)
            {
                foreach (IFormFile image in files)
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), @"wwwroot\assets\ProductImage", image.FileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                       await image.CopyToAsync(fileStream);
                    }
                }
            }
        }
        [HttpGet]
        public async Task<IActionResult> ChangeStatus(int id)
        {
            var result = await _productApiClient.ChangeStatus(id);
            if (result == null)
            {
                ModelState.AddModelError("Cập nhật thất bại", result.Message);
            }
            if (result.IsSuccess)
            {
                TempData["result"] = "Tắt top bán chạy thành công";
                return RedirectToAction("Index");
            }
            return View("Index");
        }
        [HttpGet]
        public async Task<IActionResult> OffBestSeller(int id)
        {
            var result = await _productApiClient.OffBestSeller(id);
            if (result == null)
            {
                ModelState.AddModelError("Cập nhật thất bại", result.Message);
            }
            if (result.IsSuccess)
            {
                TempData["result"] = "Tắt top bán chạy thành công";
                return RedirectToAction("Index");
            }
            return View("Index");
        }
        [HttpGet]
        public async Task<IActionResult> OffFeatured(int id)
        {
            var result = await _productApiClient.OffFeautured(id);
            if (result == null)
            {
                ModelState.AddModelError("Cập nhật thất bại", result.Message);
            }
            if (result.IsSuccess)
            {
                TempData["result"] = "Tắt top nổi bật thành công";
                return RedirectToAction("Index");
            }
            return View("Index");
        }
        [HttpPost]
        public async Task<JsonResult> DeleteImage(int id, string fileName)
        {
            var result = await _productApiClient.DeleteImage(id, fileName);
            if (result.IsSuccess)
                return Json(new { success = true, message = "Xóa hình ảnh thành công" });
            return Json(new { success = false, message = result.Message });
        }
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _productApiClient.Delete(id);
            if (result == null)
            {
                ModelState.AddModelError("", result.Message);
            }
            if (result.IsSuccess)
            {
                TempData["result"] = "Xóa sản phẩm thành công";
                return RedirectToAction("Index");
            }
            return View("Index");
        }
        [AcceptVerbs("GET", "POST")]
        public async Task<IActionResult> isValidSlug(string code, string slug)
        {
            if (await _productApiClient.isValidSlug(code, slug) == false)
            {
                return Json($"Đường dẫn {slug} đã được sử dụng.");
            }
            return Json(true);
        }
    }
}