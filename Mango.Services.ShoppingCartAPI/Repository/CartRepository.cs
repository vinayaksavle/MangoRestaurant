using AutoMapper;
using Mango.Services.ShoppingCartAPI.DbContexts;
using Mango.Services.ShoppingCartAPI.Models;
using Mango.Services.ShoppingCartAPI.Models.Dto;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mango.Services.ShoppingCartAPI.Repository
{
    public class CartRepository : ICartRepository
    {
        private readonly ApplicationDbContext _db;
        private IMapper _mapper;

        public CartRepository(ApplicationDbContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }

        public async Task<CartDto> GetCartByUserId(string userId)
        {
            Cart cart = new()
            {
                CartHeader = await _db.CartHeaders.FirstOrDefaultAsync(x => x.UserId == userId)
            };

            cart.CartDetails = _db.CartDetails.Where(x => x.CartHeaderId == cart.CartHeader.CartHeaderId).Include(x => x.Product);

            return _mapper.Map<CartDto>(cart);
        }

        public async Task<CartDto> CreateUpdateCart(CartDto cartDto)
        {
            Cart cart = _mapper.Map<Cart>(cartDto);

            // Check if product exist in database, if not create it!
            var prodInDb = await _db.Products.FirstOrDefaultAsync(x => x.ProductId == cartDto.CartDetails.FirstOrDefault().ProductId);

            if (prodInDb == null)
            {
                _db.Products.Add(cart.CartDetails.FirstOrDefault().Product);
                await _db.SaveChangesAsync();
            }

            // Check if header is null
            var cartHeaderFromDb = await _db.CartHeaders.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == cart.CartHeader.UserId);

            if (cartHeaderFromDb == null)
            {
                // Create header and details
                _db.CartHeaders.Add(cart.CartHeader);
                await _db.SaveChangesAsync();
                cart.CartDetails.FirstOrDefault().CartHeaderId = cart.CartHeader.CartHeaderId;
                cart.CartDetails.FirstOrDefault().Product = null;
                _db.CartDetails.Add(cart.CartDetails.FirstOrDefault());
                await _db.SaveChangesAsync();
            }
            else
            {
                // if header is not null 
                // check if details has same product
                var cartDetailsFromDb = await _db.CartDetails.AsNoTracking().FirstOrDefaultAsync(x => x.ProductId == cart.CartDetails.FirstOrDefault().ProductId && x.CartHeaderId == cartHeaderFromDb.CartHeaderId);

                if (cartDetailsFromDb == null)
                {
                    // Create details
                    cart.CartDetails.FirstOrDefault().CartHeaderId = cartHeaderFromDb.CartHeaderId;
                    cart.CartDetails.FirstOrDefault().Product = null;
                    _db.CartDetails.Add(cart.CartDetails.FirstOrDefault());
                    await _db.SaveChangesAsync();
                }
                else
                {
                    // Update the count / cart details
                    cart.CartDetails.FirstOrDefault().Product = null;
                    cart.CartDetails.FirstOrDefault().Count += cartDetailsFromDb.Count;
                    _db.CartDetails.Update(cart.CartDetails.FirstOrDefault());
                    await _db.SaveChangesAsync();
                }
            }

            return _mapper.Map<CartDto>(cart);
        }

        public async Task<bool> RemoveFromCart(int cartDetailsId)
        {
            try
            {
                CartDetails cartDetails = await _db.CartDetails.FirstOrDefaultAsync(x => x.CartDetailsId == cartDetailsId);

                int totalCountOfCartItems = _db.CartDetails.Where(x => x.CartHeaderId == cartDetails.CartHeaderId).Count();

                _db.CartDetails.Remove(cartDetails);
                if (totalCountOfCartItems == 1)
                {
                    var cartHeaderToRemove = await _db.CartHeaders.FirstOrDefaultAsync(x => x.CartHeaderId == cartDetails.CartHeaderId);

                    _db.CartHeaders.Remove(cartHeaderToRemove);
                }

                await _db.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> ClearCart(string userId)
        {
            var cartHeaderFromDb = await _db.CartHeaders.FirstOrDefaultAsync(x => x.UserId == userId);

            if (cartHeaderFromDb != null)
            {
                _db.CartDetails.RemoveRange(_db.CartDetails.Where(x => x.CartHeaderId == cartHeaderFromDb.CartHeaderId));
                _db.CartHeaders.Remove(cartHeaderFromDb);
                await _db.SaveChangesAsync();
                return true;
            }

            return false;
        }
    }
}
