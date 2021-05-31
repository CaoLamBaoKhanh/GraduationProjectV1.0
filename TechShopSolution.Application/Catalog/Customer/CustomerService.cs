﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechShopSolution.Data.EF;
using TechShopSolution.Utilities.Exceptions;
using TechShopSolution.ViewModels.Catalog.Customer;
using TechShopSolution.ViewModels.Common;

namespace TechShopSolution.Application.Catalog.Customer
{
    public class CustomerService : ICustomerService
    {
        private readonly TechShopDBContext _context;
        public CustomerService(TechShopDBContext context)
        {
            _context = context;
        }
        public async Task<ApiResult<bool>> Create(CustomerCreateRequest request)
        {
            try
            {
                string addressCustomer = request.House + " " + request.Ward + ", " + request.District + ", " + request.City;
                var customer = new TechShopSolution.Data.Entities.Customer
                {
                    name = request.name,
                    address = addressCustomer,
                    birthday = request.birthday,
                    email = request.email,
                    password = request.password,
                    phone = request.phone,
                    sex = request.sex,
                    status = request.status,
                    Order = new List<Data.Entities.Order>(),
                    create_at = DateTime.Now
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
                return new ApiSuccessResult<bool>();
            }
            catch
            {
                return new ApiErrorResult<bool>("Thêm thất bại");
            }
        }

        public Task<int> Delete(int cusID)
        {
            throw new NotImplementedException();
        }

        public async Task<PagedResult<CustomerViewModel>> GetAllPaging(GetCustomerPagingRequest request)
        {
            var query = from cus in _context.Customers
                        select cus;

            if (!String.IsNullOrEmpty(request.Keyword))
            {
                query = query.Where(x => x.name.Contains(request.Keyword) || x.phone.Contains(request.Keyword) || x.email.Contains(request.Keyword));
            }

            int totalRow = await query.CountAsync();

            var data = query.Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(a => new CustomerViewModel()
                {
                    id = a.id,
                    address = a.address,
                    birthday = a.birthday,
                    email = a.email,
                    name = a.name,
                    password = a.password,
                    phone = a.phone,
                    sex = a.sex,
                    status = a.status,
                }).ToListAsync();

            var pageResult = new PagedResult<CustomerViewModel>()
            {
                TotalRecord = totalRow,
                Items = await data,
            };
            return pageResult;
        }

        public async Task<ApiResult<CustomerViewModel>> GetById(int customertId)
        {
            var cusExist = await _context.Customers.FindAsync(customertId);
            if(cusExist == null)
            {
                return new ApiErrorResult<CustomerViewModel>("Khách hàng không tồn tại");
            }
            var customer = new CustomerViewModel()
            {
                name = cusExist.name,
                address = cusExist.address,
                birthday = cusExist.birthday,
                email = cusExist.email,
                password = cusExist.password,
                phone = cusExist.phone,
                sex = cusExist.sex,
                status = cusExist.status,
                id = cusExist.id
            };
            return new ApiSuccessResult<CustomerViewModel>(customer);

        }

        public async Task<ApiResult<bool>> Update(CustomerUpdateRequest request)
        {
            try
            {
                if (await _context.Customers.AnyAsync(x => x.email == request.email && x.id != request.Id))
                {
                    return new ApiErrorResult<bool>("Emai đã tồn tại");
                }
                var cusExist = await _context.Customers.FindAsync(request.Id);
                if(cusExist!=null)
                {
                    cusExist.email = request.email;
                    cusExist.name = request.name;
                    cusExist.phone = request.phone;
                    cusExist.status = request.status;
                    cusExist.update_at = DateTime.Now;
                }
                await _context.SaveChangesAsync();
                return new ApiSuccessResult<bool>();
            }
            catch
            {
                return new ApiErrorResult<bool>("Cập nhật thất bại");
            }
        }

        public async Task<ApiResult<bool>> UpdateAddress(int id, CustomerUpdateAddressRequest request)
        {
            try
            {
                var cusExist = await _context.Customers.FindAsync(id);
                if (cusExist != null)
                {
                    string newAddress = request.House + " " + request.Ward + ", " + request.District + ", " + request.City;
                    cusExist.address = newAddress;
                    cusExist.update_at = DateTime.Now;
                    await _context.SaveChangesAsync();
                    return new ApiSuccessResult<bool>();
                }
                else return new ApiErrorResult<bool>("Không tìm thấy khách hàng này");
            }
            catch
            {
                return new ApiErrorResult<bool>("Cập nhật thất bại");
            }
        }

        public async Task<bool> VerifyEmail(string email)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(x => x.email.Equals(email));
            if(customer!=null)
            {
                return false;
            }
            return true;
        }
    }
}
