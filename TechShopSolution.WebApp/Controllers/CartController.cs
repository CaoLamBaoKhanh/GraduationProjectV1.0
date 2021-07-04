﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using TechShopSolution.ApiIntegration;
using TechShopSolution.Utilities.Constants;
using TechShopSolution.ViewModels.Catalog.Customer;
using TechShopSolution.ViewModels.Common;
using TechShopSolution.ViewModels.Sales;
using TechShopSolution.WebApp.Models;

namespace TechShopSolution.WebApp.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly IProductApiClient _productApiClient;
        private readonly ICouponApiClient _couponApiClient;
        private readonly ICustomerApiClient _customerApiClient;
        private readonly IPaymentApiClient _paymentApiClient;
        private readonly IOrderApiClient _orderApiClient;

        public CartController(IProductApiClient productApiClient, ICouponApiClient couponApiClient,
            ICustomerApiClient customerApiClient, IPaymentApiClient paymentApiClient, IOrderApiClient orderApiClient)
        {
            _productApiClient = productApiClient;
            _couponApiClient = couponApiClient;
            _customerApiClient = customerApiClient;
            _paymentApiClient = paymentApiClient;
            _orderApiClient = orderApiClient;
        }
        [AllowAnonymous]
        [Route("/gio-hang")]
        public IActionResult Index()
        {
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> Checkout(string id)
        {
            var customer = await _customerApiClient.GetById(int.Parse(id));
            if (customer.ResultObject != null)
            {
                ViewBag.CustomerAddress = customer.ResultObject.address;
            }
            ViewBag.Payment = await _paymentApiClient.GetAll();
            var session = HttpContext.Session.GetString(SystemConstants.CartSession);
            CartViewModel currentCart = new CartViewModel();
            List<CreateOrderDetailRequest> OrderDetail = new List<CreateOrderDetailRequest>();
            if (session != null)
                currentCart = JsonConvert.DeserializeObject<CartViewModel>(session);
            int? coupon_id = null; decimal amount = 0; decimal total = 0; decimal discount = 0;

            foreach (var item in currentCart.items)
            {
                if (item.PromotionPrice > 0)
                    amount = item.Quantity * item.PromotionPrice;
                else amount = item.Quantity * item.Price;
                total += amount;
                var detail = new CreateOrderDetailRequest
                {
                    product_id = item.Id,
                    promotion_price = item.PromotionPrice,
                    quantity = item.Quantity,
                    image = item.Images,
                    name = item.Name,
                    slug = item.Slug,
                    unit_price = item.Price
                };
                OrderDetail.Add(detail);
            }
            if (currentCart.coupon != null)
            {
                var coupon = await _couponApiClient.GetByCode(currentCart.coupon.code);
                if (coupon.ResultObject != null)
                    coupon_id = coupon.ResultObject.id;
                if (coupon.ResultObject.min_order_value != null)
                {
                    if ((decimal)coupon.ResultObject.min_order_value <= total)
                    {
                        if (coupon.ResultObject.type.Equals("Phần trăm"))
                        {
                            if (coupon.ResultObject.max_price != null)
                            {
                                discount = total * ((decimal)coupon.ResultObject.value / 100);
                                if (discount > (decimal)coupon.ResultObject.max_price)
                                    discount = (decimal)coupon.ResultObject.max_price;
                            }
                            else discount = total * ((decimal)coupon.ResultObject.value / 100);
                        }
                        else discount = (decimal)coupon.ResultObject.value;
                    }
                }
                else
                {
                    if (coupon.ResultObject.type.Equals("Phần trăm"))
                    {
                        if (coupon.ResultObject.max_price != null)
                        {
                            discount = total * ((decimal)coupon.ResultObject.value / 100);
                            if (discount > (decimal)coupon.ResultObject.max_price)
                                discount = (decimal)coupon.ResultObject.max_price;
                        }
                        else discount = total * ((decimal)coupon.ResultObject.value / 100);
                    }
                    else discount = (decimal)coupon.ResultObject.value;
                }
            }
            return View(new CheckoutRequest
            {
                Order = new CreteOrderRequest
                {
                    address_receiver = customer.ResultObject.address,
                    coupon_id = coupon_id,
                    cus_id = customer.ResultObject.id,
                    total = total,
                    discount = discount,
                    name_receiver = customer.ResultObject.name,
                    note = null,
                    phone_receiver = customer.ResultObject.phone,
                },
                OrderDetails = OrderDetail
            });
        }
        [HttpPost]
        public async Task<IActionResult> Checkout(CheckoutRequest request)
        {
            var session = HttpContext.Session.GetString(SystemConstants.CartSession);
            CartViewModel currentCart = new CartViewModel();
            List<CreateOrderDetailRequest> OrderDetail = new List<CreateOrderDetailRequest>();
            if (session != null)
                currentCart = JsonConvert.DeserializeObject<CartViewModel>(session);

            foreach (var item in currentCart.items)
            {
                var detail = new CreateOrderDetailRequest
                {
                    product_id = item.Id,
                    promotion_price = item.PromotionPrice,
                    quantity = item.Quantity,
                    image = item.Images,
                    name = item.Name,
                    slug = item.Slug,
                    unit_price = item.Price
                };
                OrderDetail.Add(detail);
            }
            request.OrderDetails = OrderDetail;
            var customer = await _customerApiClient.GetById(request.Order.cus_id);
            if (customer.ResultObject != null)
            {
                ViewBag.CustomerAddress = customer.ResultObject.address;
            }
            ViewBag.Payment = await _paymentApiClient.GetAll();
            if (request.Order.payment_id == 0)
                request.Order.payment_id = null;
            if (!ModelState.IsValid)
                return View(request);
            var result = await _orderApiClient.CreateOrder(request);
            if(result.IsSuccess)
            {
                TempData["result"] = "Đặt hàng thành công. Cảm ơn quý khách đã mua hàng của chúng tôi.";
                HttpContext.Session.Remove(SystemConstants.CartSession);
                var contentMailClient = sendMailToClient(int.Parse(result.ResultObject), request);
                var contentMailAdmin = sendMailToAdmin(int.Parse(result.ResultObject), request, customer.ResultObject);
                await SendMail("thuanneuwu2@gmail.com", customer.ResultObject.email, "Đặt hàng thành công - Đơn hàng #" + result.ResultObject, contentMailClient, "thuanneuwu2@gmail.com", "thanhthuan123");
                await SendMail("thuanneuwu2@gmail.com", "thuanneuwu2@gmail.com", "Đơn hàng mới #" + result.ResultObject, contentMailAdmin, "thuanneuwu2@gmail.com", "thanhthuan123");
                return RedirectToAction("Index","Home");
            }
            ModelState.AddModelError("", result.Message);
            return View(request);
        }
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetListItems()
        {
            var session = HttpContext.Session.GetString(SystemConstants.CartSession);
            CartViewModel currentCart = new CartViewModel();
            if (session != null)
                currentCart = JsonConvert.DeserializeObject<CartViewModel>(session);
            if (currentCart.coupon != null)
            {
                var result = await _couponApiClient.GetByCode(currentCart.coupon.code);
                currentCart.coupon = new CouponViewModel
                {
                    code = result.ResultObject.code,
                    type = result.ResultObject.type,
                    value = result.ResultObject.value,
                    max_value = result.ResultObject.max_price,
                    min_order_value = result.ResultObject.min_order_value,
                    quantity = result.ResultObject.quantity
                };
            }
            return Ok(currentCart);
        }
        [AllowAnonymous]
        public async Task<IActionResult> AddToCart(int id)
        {
            var product = await _productApiClient.GetById(id);
            if (product.ResultObject == null)
                return BadRequest("Thêm vào giỏ hàng thất bại ! Sản phẩm không tồn tại hoặc đã bị xóa.");
            var session = HttpContext.Session.GetString(SystemConstants.CartSession);
            var currentCart = new CartViewModel();
            currentCart.items = new List<CartItemViewModel>();
            if (session != null)
                currentCart = JsonConvert.DeserializeObject<CartViewModel>(session);
            int quantity = 1;
            if (currentCart.items.Any(x => x.Id == product.ResultObject.id))
            {
                var item = currentCart.items.First(x => x.Id == id);
                if (item.Quantity >= 5)
                    return BadRequest("Bạn chỉ được mua tối đa 5 sản phẩm, sản phẩm này đã có trong giỏ hàng của bạn.");
                else item.Quantity++;
            }
            else
            {
                var cartItem = new CartItemViewModel()
                {
                    Id = id,
                    Instock = product.ResultObject.instock,
                    Code = product.ResultObject.code,
                    Slug = product.ResultObject.slug,
                    Price = product.ResultObject.unit_price,
                    PromotionPrice = product.ResultObject.promotion_price,
                    Images = product.ResultObject.image,
                    Name = product.ResultObject.name,
                    Quantity = quantity
                };
                if (currentCart.items == null) currentCart.items = new List<CartItemViewModel>();
                currentCart.items.Add(cartItem);

            }
            HttpContext.Session.SetString(SystemConstants.CartSession, JsonConvert.SerializeObject(currentCart));

            return Ok(currentCart);
        }
        [AllowAnonymous]
        public async Task<IActionResult> UpdateCart(int id, int quantity)
        {
            var session = HttpContext.Session.GetString(SystemConstants.CartSession);
            CartViewModel currentCart = new CartViewModel();
            if (session != null)
                currentCart = JsonConvert.DeserializeObject<CartViewModel>(session);
            foreach (var item in currentCart.items)
            {
                if (item.Id == id)
                {
                    if (quantity == 0)
                    {
                        currentCart.items.Remove(item);
                        break;
                    }
                    item.Quantity = quantity;
                    break;

                }
            }
            if (currentCart.coupon != null)
            {
                var result = await _couponApiClient.GetByCode(currentCart.coupon.code);
                currentCart.coupon = new CouponViewModel
                {
                    code = result.ResultObject.code,
                    type = result.ResultObject.type,
                    value = result.ResultObject.value,
                    max_value = result.ResultObject.max_price,
                    min_order_value = result.ResultObject.min_order_value,
                    quantity = result.ResultObject.quantity
                };
            }
            HttpContext.Session.SetString(SystemConstants.CartSession, JsonConvert.SerializeObject(currentCart));
            return Ok(currentCart);
        }
        [AllowAnonymous]
        public async Task<IActionResult> UseCoupon(string code)
        {
            var session = HttpContext.Session.GetString(SystemConstants.CartSession);
            if (session == null || session == "")
            {
                return BadRequest("Chưa có sản phẩm nào trong giỏ hàng.");
            }
            else
            {
                var result = await _couponApiClient.GetByCode(code);
                if (result.ResultObject == null)
                    return BadRequest("Mã giảm giá không tồn tại");
                if (result.ResultObject.start_at > DateTime.Today)
                {
                    return BadRequest("Mã này sẽ có hiệu lực lúc " + result.ResultObject.start_at + ". Hãy thử lại sau bạn nhé");
                }
                if (result.ResultObject.end_at < DateTime.Today)
                {
                    return BadRequest("Mã này đã hết hạn");
                }
                if (!result.ResultObject.isActive)
                    return BadRequest("Mã này đã bị vô hiệu hóa");
                if (result.ResultObject.quantity == 0)
                    return BadRequest("Mã này đã được sử dụng hết");

                CartViewModel currentCart = new CartViewModel();
                currentCart = JsonConvert.DeserializeObject<CartViewModel>(session);
                if (result.ResultObject.min_order_value != null)
                {
                    decimal total = 0;
                    decimal amount = 0;
                    foreach (var item in currentCart.items)
                    {
                        if (item.PromotionPrice > 0)
                        {
                            amount = item.PromotionPrice * item.Quantity;
                        }
                        else
                        {
                            amount = item.Price * item.Quantity;
                        }
                        total += amount;
                    }
                    if (total < (decimal)result.ResultObject.min_order_value)
                    {
                        var value = (decimal)result.ResultObject.min_order_value - total;
                        var info = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
                        return BadRequest("Chưa đạt giá trị đơn hàng tối thiểu. Cần mua thêm " + String.Format(info, "{0:c}", value) + " để sử dụng mã này");
                    }
                }

                currentCart.coupon = new CouponViewModel
                {
                    code = result.ResultObject.code,
                    type = result.ResultObject.type,
                    value = result.ResultObject.value,
                    max_value = result.ResultObject.max_price,
                    min_order_value = result.ResultObject.min_order_value,
                    quantity = result.ResultObject.quantity
                };

                HttpContext.Session.SetString(SystemConstants.CartSession, JsonConvert.SerializeObject(currentCart));

                return Ok(currentCart);
            }

        }
        [AllowAnonymous]
        public async Task<JsonResult> LoadProvince()
        {
            try
            {
                var result = await _customerApiClient.LoadProvince();
                if (result == null || !result.IsSuccess)
                {
                    return null;
                }
                return Json(new
                {
                    data = result.ResultObject,
                    status = true
                });
            }
            catch
            {
                return null;
            }
        }
        [AllowAnonymous]
        public async Task<JsonResult> LoadDistrict(int provinceID)
        {
            try
            {
                var result = await _customerApiClient.LoadDistrict(provinceID);
                if (result == null || !result.IsSuccess)
                {
                    return null;
                }
                return Json(new
                {
                    data = result.ResultObject,
                    status = true
                });
            }
            catch
            {
                return null;
            }
        }
        [AllowAnonymous]
        public async Task<JsonResult> LoadWard(int districtID)
        {
            try
            {
                var result = await _customerApiClient.LoadWard(districtID);
                if (result == null || !result.IsSuccess)
                {
                    return null;
                }
                return Json(new
                {
                    data = result.ResultObject,
                    status = true
                });
            }
            catch
            {
                return null;
            }
        }
        public string sendMailToClient(int orderID, CheckoutRequest request)
        {
            var info = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
            string contentMail = System.IO.File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), @"wwwroot\mail-template", "neworder.html"));
            contentMail = contentMail.Replace("{{order_id}}", orderID.ToString());
            contentMail = contentMail.Replace("{{total}}", String.Format(info, "{0:N0}", request.Order.total));
            contentMail = contentMail.Replace("{{discount}}", String.Format(info, "{0:N0}", request.Order.discount));
            contentMail = contentMail.Replace("{{ship_fee}}", String.Format(info, "{0:N0}", request.Order.transport_fee));
            contentMail = contentMail.Replace("{{address}}", String.Format(info, "{0:N0}", request.Order.address_receiver));
            var final_total = request.Order.total - request.Order.discount + request.Order.transport_fee;
            contentMail = contentMail.Replace("{{final_total}}", String.Format(info, "{0:N0}", final_total));
            return contentMail;
        }
        public string sendMailToAdmin(int orderID, CheckoutRequest request, CustomerViewModel cutomer)
        {
            var info = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
            string contentMail = System.IO.File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), @"wwwroot\mail-template", "AdminOrderConfirm.html"));
            contentMail = contentMail.Replace("{{order_id}}", orderID.ToString());
            contentMail = contentMail.Replace("{{customer_name}}", request.Order.name_receiver);
            contentMail = contentMail.Replace("{{customer_email}}", cutomer.email);
            contentMail = contentMail.Replace("{{customer_phone}}", request.Order.phone_receiver);
            contentMail = contentMail.Replace("{{order_note}}", request.Order.note);
            contentMail = contentMail.Replace("{{order_time}}", DateTime.Now.ToString());
            contentMail = contentMail.Replace("{{total}}", String.Format(info, "{0:N0}", request.Order.total));
            contentMail = contentMail.Replace("{{discount}}", String.Format(info, "{0:N0}", request.Order.discount));
            contentMail = contentMail.Replace("{{ship_fee}}", String.Format(info, "{0:N0}", request.Order.transport_fee));
            contentMail = contentMail.Replace("{{address}}", String.Format(info, "{0:N0}", request.Order.address_receiver));
            var final_total = request.Order.total - request.Order.discount + request.Order.transport_fee;
            contentMail = contentMail.Replace("{{final_total}}", String.Format(info, "{0:N0}", final_total));
            return contentMail;
        }
        public async Task SendMail(string _from, string _to, string _subject, string _body, string _gmail, string _password)
        {
            MailMessage message = new MailMessage(_from, _to, _subject, _body);
            message.BodyEncoding = System.Text.Encoding.UTF8;
            message.SubjectEncoding = System.Text.Encoding.UTF8;
            message.IsBodyHtml = true;

            message.ReplyToList.Add(new MailAddress(_from));
            message.Sender = new MailAddress(_from);

            using var smtpClient = new SmtpClient("smtp.gmail.com");
            smtpClient.Port = 587;
            smtpClient.EnableSsl = true;
            smtpClient.Credentials = new NetworkCredential(_gmail, _password);

            await smtpClient.SendMailAsync(message);
        }
    }
}
