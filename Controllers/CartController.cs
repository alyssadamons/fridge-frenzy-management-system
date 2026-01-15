using E_Commerce.Areas.Dashboard.Data;
using E_Commerce.Areas.Dashboard.Models;
using E_Commerce.Client;
using E_Commerce.Data;
using E_Commerce.Helper;
using E_Commerce.Models;
using E_Commerce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

namespace E_Commerce.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CartController> _logger;
        private readonly PayPalClient _payPalClient;

        public CartController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<CartController> logger,
            PayPalClient payPalClient)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            QuestPDF.Settings.License = LicenseType.Community;
            _payPalClient = payPalClient;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(HttpContext.User);
                if (currentUser == null)
                {
                    return View(new List<Cart>());
                }

                var cart = await _context.Carts
                    .Include(c => c.Product)
                    .Where(x => x.UserId == currentUser.Id)
                    .ToListAsync();

                return View(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading cart for user");
                TempData["Error"] = "Error loading cart. Please try again.";
                return View(new List<Cart>());
            }
        }

        public async Task<IActionResult> GetCartCount()
        {
            var currentUser = await _userManager.GetUserAsync(HttpContext.User);
            if (currentUser == null)
            {
                return Json(new { count = 0 });
            }

            var cartCount = await _context.Carts
                .Where(c => c.UserId == currentUser.Id)
                .SumAsync(c => c.Qty);
            return Json(new { count = cartCount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int qty = 1)
        {
            try
            {
                _logger.LogInformation("AddToCart called for ProductId: {ProductId}, Qty: {Qty}", productId, qty);

                var currentUser = await _userManager.GetUserAsync(HttpContext.User);
                if (currentUser == null)
                {
                    return Json(new { success = false, message = "Please login to add items to cart" });
                }

                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    return Json(new { success = false, message = "Product not found" });
                }

                var existingCartItem = await _context.Carts
                    .FirstOrDefaultAsync(c => c.UserId == currentUser.Id && c.ProductId == productId);

                if (existingCartItem != null)
                {
                    existingCartItem.Qty += qty;
                    _context.Carts.Update(existingCartItem);
                }
                else
                {
                    var maxId = await _context.Carts.AnyAsync()
                        ? await _context.Carts.MaxAsync(c => c.Id)
                        : 0;

                    var newCartItem = new Cart
                    {
                        Id = maxId + 1,
                        ProductId = productId,
                        Qty = qty,
                        UserId = currentUser.Id
                    };
                    await _context.Carts.AddAsync(newCartItem);
                }

                await _context.SaveChangesAsync();

                var cartCount = await _context.Carts
                    .Where(c => c.UserId == currentUser.Id)
                    .SumAsync(c => c.Qty);

                return Json(new
                {
                    success = true,
                    message = $"{product.Name} added to cart!",
                    cartCount = cartCount
                });
            }
            catch (DbUpdateException dbEx)
            {
                var innerException = dbEx.InnerException;
                var errorMessage = innerException?.Message ?? dbEx.Message;

                _logger.LogError(dbEx, "DATABASE ERROR in AddToCart: {ErrorMessage}", errorMessage);

                return Json(new
                {
                    success = false,
                    message = $"Database error: {errorMessage}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CART ERROR: {Message}", ex.Message);
                return Json(new
                {
                    success = false,
                    message = "An error occurred while adding to cart. Please try again."
                });
            }
        }

        public async Task<IActionResult> UpdateQuantity(int cartId, int quantity)
        {
            var currentUser = await _userManager.GetUserAsync(HttpContext.User);
            if (currentUser == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (quantity < 1)
            {
                return RedirectToAction("RemoveFromCart", new { cartId = cartId });
            }

            var cartItem = await _context.Carts
                .FirstOrDefaultAsync(c => c.Id == cartId && c.UserId == currentUser.Id);

            if (cartItem != null)
            {
                cartItem.Qty = quantity;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> RemoveFromCart(int cartId)
        {
            var currentUser = await _userManager.GetUserAsync(HttpContext.User);
            if (currentUser == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var cartItem = await _context.Carts
                .FirstOrDefaultAsync(c => c.Id == cartId && c.UserId == currentUser.Id);

            if (cartItem != null)
            {
                _context.Carts.Remove(cartItem);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult SetDeliveryOption(string deliveryOption)
        {
            if (deliveryOption == "Express" || deliveryOption == "Standard")
            {
                HttpContext.Session.SetString("DeliveryOption", deliveryOption);
                return Ok();
            }
            return BadRequest();
        }

        [HttpPost]
        public IActionResult SetExchangeRate([FromBody] ExchangeRateRequest request)
        {
            if (request != null && request.ExchangeRate > 0)
            {
                HttpContext.Session.SetString("CurrentExchangeRate", request.ExchangeRate.ToString());
                _logger.LogInformation("💱 Exchange rate set: {ExchangeRate}", request.ExchangeRate);
                return Ok();
            }
            return BadRequest();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var currentUser = await _userManager.GetUserAsync(HttpContext.User);
            if (currentUser == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            _logger.LogInformation("Checkout called for user: {UserId}", currentUser.Id);

            var cartItems = await _context.Carts
                .Include(c => c.Product)
                .Where(x => x.UserId == currentUser.Id)
                .ToListAsync();

            _logger.LogInformation("Cart items count: {Count}", cartItems.Count);

            if (!cartItems.Any())
            {
                _logger.LogWarning("Empty cart for user: {UserId}", currentUser.Id);
                return RedirectToAction("Index");
            }

            var customer = await GetCurrentCustomerAsync(currentUser);
            _logger.LogInformation("Customer found: {CustomerFound}", customer != null);

            if (customer == null)
            {
                TempData["Error"] = "Customer profile not found. Please update your profile first.";
                return RedirectToAction("Checkout");
            }

            var deliveryOption = HttpContext.Session.GetString("DeliveryOption") ?? "Standard";
            decimal deliveryFee = deliveryOption == "Express" ? 1000m : 500m;

            string fullAddress = BuildCustomerAddress(customer);

            var subtotal = cartItems.Sum(x => x.Qty * x.Product.Price);
            var total = subtotal + deliveryFee;

            var checkoutViewModel = new CheckoutViewModel
            {
                CartItems = cartItems,
                SubTotal = subtotal,
                DeliveryFee = deliveryFee,
                Total = total,
                UserEmail = currentUser.Email,
                CustomerName = customer?.Name ?? currentUser.UserName,
                CustomerPhone = customer?.ContactNumber ?? "",
                DeliveryAddress = fullAddress
            };

            return View(checkoutViewModel);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessCheckout(CheckoutViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(HttpContext.User);
            if (currentUser == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            _logger.LogInformation("ProcessCheckout called for user: {UserId}", currentUser.Id);

            try
            {
                var cartItems = await _context.Carts
                    .Include(c => c.Product)
                    .Where(x => x.UserId == currentUser.Id)
                    .ToListAsync();

                _logger.LogInformation("Cart items count in ProcessCheckout: {Count}", cartItems.Count);

                if (!cartItems.Any())
                {
                    TempData["Error"] = "Your cart is empty.";
                    return RedirectToAction("Index");
                }

                var customer = await GetCurrentCustomerAsync(currentUser);
                if (customer == null)
                {
                    TempData["Error"] = "Customer profile not found. Please update your profile first.";
                    _logger.LogWarning("Customer not found for user: {UserId}", currentUser.Id);
                    return RedirectToAction("Checkout"); // CHANGED HERE
                }

                string fullAddress = BuildCustomerAddress(customer);

                if (string.IsNullOrEmpty(model.PaymentMethod))
                {
                    ModelState.AddModelError("PaymentMethod", "Please select a payment method.");
                    _logger.LogWarning("Payment method not selected for user: {UserId}", currentUser.Id);
                    return RedirectToAction("Checkout"); // CHANGED HERE
                }

                if (model.PaymentMethod == "PayPal")
                {
                    TempData["Error"] = "Please use the PayPal button to complete payment.";
                    return RedirectToAction("Checkout"); // CHANGED HERE
                }

                var deliveryOption = HttpContext.Session.GetString("DeliveryOption") ?? "Standard";
                decimal deliveryFee = deliveryOption == "Express" ? 1000m : 500m;

                _logger.LogInformation("Creating order for customer: {CustomerId}", customer.CustomerID);

                var subtotal = cartItems.Sum(x => x.Qty * x.Product.Price);
                var total = subtotal + deliveryFee;

                var order = new Order
                {
                    CustomerID = customer.CustomerID,
                    UserId = currentUser.Id,
                    OrderDate = DateTime.Now,
                    SubTotal = subtotal,
                    DeliveryFee = deliveryFee,
                    Total = total,
                    Status = "Pending",
                    DeliveryAddress = fullAddress,
                    CustomerName = customer.Name,
                    CustomerPhone = customer.ContactNumber,
                    PaymentMethod = model.PaymentMethod
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                var orderId = order.Id;
                _logger.LogInformation("Order created with ID: {OrderId}", orderId);

                foreach (var cartItem in cartItems)
                {
                    var orderItem = new OrderItem
                    {
                        OrderId = orderId,
                        ProductId = cartItem.ProductId,
                        Quantity = cartItem.Qty,
                        Price = cartItem.Product.Price
                    };
                    _context.OrderItems.Add(orderItem);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Order items created for order: {OrderId}", orderId);

                _context.Carts.RemoveRange(cartItems);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cart cleared for user: {UserId}", currentUser.Id);

                HttpContext.Session.Remove("DeliveryOption");

                _logger.LogInformation("Redirecting to OrderConfirmation with orderId: {OrderId}", orderId);
                return RedirectToAction("OrderConfirmation", new { orderId = orderId });
            }
            catch (DbUpdateException dbEx)
            {
                var innerException = dbEx.InnerException;
                var errorMessage = innerException?.Message ?? dbEx.Message;

                _logger.LogError(dbEx, "Database error during checkout for user {UserId}. Error: {ErrorMessage}",
                    currentUser.Id, errorMessage);
                TempData["Error"] = $"A database error occurred during checkout: {errorMessage}";
                return RedirectToAction("Checkout"); // CHANGED HERE
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checkout error for user {UserId}", currentUser.Id);
                TempData["Error"] = $"An error occurred during checkout: {ex.Message}";
                return RedirectToAction("Checkout"); // CHANGED HERE
            }
        }
        // ===== PAYPAL METHODS =====

        private async Task<string> GetPayPalAccessToken()
        {
            try
            {
                if (string.IsNullOrEmpty(_payPalClient.ClientId) || string.IsNullOrEmpty(_payPalClient.ClientSecret))
                {
                    _logger.LogError("PayPal credentials are missing! ClientId: {ClientId}, Secret: {HasSecret}",
                        _payPalClient.ClientId, !string.IsNullOrEmpty(_payPalClient.ClientSecret));
                    return null;
                }

                _logger.LogInformation("Requesting PayPal access token...");

                var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_payPalClient.ClientId}:{_payPalClient.ClientSecret}"));

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

                var requestBody = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

                var response = await client.PostAsync($"{_payPalClient.BaseUrl}/v1/oauth2/token", requestBody);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var json = JObject.Parse(responseContent);
                    var token = json["access_token"]?.ToString();
                    _logger.LogInformation("✅ PayPal access token obtained successfully");
                    return token;
                }

                _logger.LogError("❌ Failed to get PayPal access token. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, responseContent);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Exception getting PayPal access token");
                return null;
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreatePayPalOrder()
        {
            try
            {
                _logger.LogInformation("🔵 CreatePayPalOrder called");

                var currentUser = await _userManager.GetUserAsync(HttpContext.User);
                if (currentUser == null)
                {
                    _logger.LogWarning("User not authenticated");
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var cartItems = await _context.Carts
                    .Include(c => c.Product)
                    .Where(x => x.UserId == currentUser.Id)
                    .ToListAsync();

                if (!cartItems.Any())
                {
                    _logger.LogWarning("Cart is empty");
                    return Json(new { success = false, message = "Cart is empty" });
                }

                var deliveryOption = HttpContext.Session.GetString("DeliveryOption") ?? "Standard";
                decimal deliveryFee = deliveryOption == "Standard" ? 500m : 1000m;
                var totalZAR = cartItems.Sum(x => x.Qty * x.Product.Price) + deliveryFee;

                var exchangeRateStr = HttpContext.Session.GetString("CurrentExchangeRate");
                decimal exchangeRate = 0.054m;

                if (!string.IsNullOrEmpty(exchangeRateStr) && decimal.TryParse(exchangeRateStr, out decimal parsedRate))
                {
                    exchangeRate = parsedRate;
                    _logger.LogInformation("💰 Using real-time exchange rate: {ExchangeRate}", exchangeRate);
                }
                else
                {
                    _logger.LogInformation("💰 Using fallback exchange rate: {ExchangeRate}", exchangeRate);
                }

                var totalUSD = Math.Round(totalZAR * exchangeRate, 2);

                _logger.LogInformation("💰 Order: ZAR {TotalZAR:N2} → USD {TotalUSD:N2} (Rate: {ExchangeRate})", totalZAR, totalUSD, exchangeRate);

                var accessToken = await GetPayPalAccessToken();
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogError("Failed to get access token");
                    return Json(new { success = false, message = "Failed to authenticate with PayPal. Check your credentials." });
                }

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var orderRequest = new
                {
                    intent = "CAPTURE",
                    purchase_units = new[]
                    {
                        new
                        {
                            amount = new
                            {
                                currency_code = "USD",
                                value = totalUSD.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                            },
                            description = "Fridge Frenzy Order"
                        }
                    }
                };

                var json = JsonConvert.SerializeObject(orderRequest, Formatting.Indented);
                _logger.LogInformation("📤 PayPal Request:\n{Json}", json);

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync($"{_payPalClient.BaseUrl}/v2/checkout/orders", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("📥 PayPal Response ({Status}):\n{Response}", response.StatusCode, responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var orderResponse = JObject.Parse(responseContent);
                    var orderId = orderResponse["id"]?.ToString();

                    HttpContext.Session.SetString("PayPalOrderId", orderId);

                    _logger.LogInformation("✅ PayPal order created: {OrderId}", orderId);

                    return Json(new { success = true, orderId = orderId });
                }
                else
                {
                    _logger.LogError("❌ PayPal order creation failed");
                    return Json(new { success = false, message = "Failed to create PayPal order", error = responseContent });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Exception in CreatePayPalOrder");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CapturePayPalOrder([FromBody] PayPalCaptureRequest request)
        {
            try
            {
                _logger.LogInformation("🔵 CapturePayPalOrder called for OrderId: {OrderId}", request.OrderId);

                var currentUser = await _userManager.GetUserAsync(HttpContext.User);
                if (currentUser == null)
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var accessToken = await GetPayPalAccessToken();
                if (string.IsNullOrEmpty(accessToken))
                {
                    return Json(new { success = false, message = "Failed to authenticate with PayPal" });
                }

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.PostAsync($"{_payPalClient.BaseUrl}/v2/checkout/orders/{request.OrderId}/capture", null);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("📥 PayPal Capture Response: {Response}", responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var captureResponse = JObject.Parse(responseContent);
                    var status = captureResponse["status"]?.ToString();

                    if (status == "COMPLETED")
                    {
                        var cartItems = await _context.Carts
                            .Include(c => c.Product)
                            .Where(x => x.UserId == currentUser.Id)
                            .ToListAsync();

                        var deliveryOption = HttpContext.Session.GetString("DeliveryOption") ?? "Standard";
                        decimal deliveryFee = deliveryOption == "Standard" ? 500m : 1000m;

                        var customer = await GetCurrentCustomerAsync(currentUser);
                        if (customer == null)
                        {
                            return Json(new { success = false, message = "Customer profile not found" });
                        }

                        string fullAddress = BuildCustomerAddress(customer);

                        var order = new Order
                        {
                            CustomerID = customer.CustomerID,
                            UserId = currentUser.Id,
                            OrderDate = DateTime.Now,
                            SubTotal = cartItems.Sum(x => x.Qty * x.Product.Price),
                            DeliveryFee = deliveryFee,
                            Total = cartItems.Sum(x => x.Qty * x.Product.Price) + deliveryFee,
                            Status = "Paid",
                            DeliveryAddress = fullAddress,
                            CustomerName = customer.Name,
                            CustomerPhone = customer.ContactNumber,
                            PaymentMethod = "PayPal",
                            PaymentReference = request.OrderId
                        };

                        _context.Orders.Add(order);
                        await _context.SaveChangesAsync();

                        foreach (var cartItem in cartItems)
                        {
                            var orderItem = new OrderItem
                            {
                                OrderId = order.Id,
                                ProductId = cartItem.ProductId,
                                Quantity = cartItem.Qty,
                                Price = cartItem.Product.Price
                            };
                            _context.OrderItems.Add(orderItem);
                        }

                        _context.Carts.RemoveRange(cartItems);
                        await _context.SaveChangesAsync();

                        HttpContext.Session.Remove("DeliveryOption");
                        HttpContext.Session.Remove("PayPalOrderId");
                        HttpContext.Session.Remove("CurrentExchangeRate");

                        _logger.LogInformation("✅ PayPal payment captured successfully. Order ID: {OrderId}", order.Id);

                        return Json(new { success = true, orderId = order.Id });
                    }
                    else
                    {
                        _logger.LogWarning("PayPal capture status: {Status}", status);
                        return Json(new { success = false, message = $"Payment status: {status}" });
                    }
                }
                else
                {
                    _logger.LogError("❌ PayPal capture failed: {Response}", responseContent);
                    return Json(new { success = false, message = "Payment capture failed" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error capturing PayPal payment");
                return Json(new { success = false, message = "An error occurred while processing payment" });
            }
        }
        [HttpPost]
        public IActionResult StoreCustomerDetails([FromBody] CustomerDetailsRequest request)
        {
            try
            {
                HttpContext.Session.SetString("CustomerName", request.CustomerName ?? "");
                HttpContext.Session.SetString("CustomerPhone", request.CustomerPhone ?? "");
                HttpContext.Session.SetString("DeliveryAddress", request.DeliveryAddress ?? "");

                _logger.LogInformation("✅ Customer details stored in session");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing customer details");
                return Json(new { success = false });
            }
        }

        [HttpGet]
        public IActionResult PayPalSuccess()
        {
            return View();
        }

        [HttpGet]
        public IActionResult PayPalCancel()
        {
            return View();
        }

        // ===== END PAYPAL METHODS =====


        [Authorize]
        [HttpGet]
        public async Task<IActionResult> OrderConfirmation(int orderId)
        {
            var currentUser = await _userManager.GetUserAsync(HttpContext.User);
            if (currentUser == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var customer = await GetCurrentCustomerAsync(currentUser);
            if (customer == null)
            {
                TempData["Error"] = "Customer profile not found.";
                return RedirectToAction("Index", "Home");
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerID == customer.CustomerID);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> OrderHistory()
        {
            var currentUser = await _userManager.GetUserAsync(HttpContext.User);
            if (currentUser == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            try
            {
                var customer = await GetCurrentCustomerAsync(currentUser);
                if (customer == null)
                {
                    TempData["Error"] = "Customer profile not found.";
                    return View(new List<Order>());
                }

                var orders = await _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .Where(o => o.CustomerID == customer.CustomerID)
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();

                return View(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading order history for user {UserId}", currentUser.Id);
                TempData["Error"] = "Error loading order history. Please try again.";
                return View(new List<Order>());
            }
        }

        [HttpGet]
        public IActionResult RedirectToFridgeRegistration(int orderId)
        {
            return RedirectToAction("RegisterFridgeFromOrder", "Services", new { area = "Identity", orderId = orderId });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> DownloadInvoice(int orderId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(HttpContext.User);
                if (currentUser == null)
                {
                    return RedirectToPage("/Account/Login", new { area = "Identity" });
                }

                var customer = await GetCurrentCustomerAsync(currentUser);
                if (customer == null)
                {
                    TempData["Error"] = "Customer profile not found.";
                    return RedirectToAction("Index", "Home");
                }

                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerID == customer.CustomerID);

                if (order == null)
                {
                    TempData["Error"] = "Order not found or you don't have permission to access it.";
                    return RedirectToAction("Index", "Home");
                }

                if (order.OrderItems == null || !order.OrderItems.Any())
                {
                    TempData["Error"] = "Cannot generate invoice for order with no items.";
                    return RedirectToAction("OrderConfirmation", new { orderId = orderId });
                }

                var pdfBytes = GenerateInvoicePdf(order);

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    throw new InvalidOperationException("PDF generation returned empty result");
                }

                var fileName = $"Invoice_Order_{order.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF invoice for order {OrderId}", orderId);
                TempData["Error"] = "An error occurred while generating the invoice. Please contact support.";
                return RedirectToAction("OrderConfirmation", new { orderId = orderId });
            }
        }

        // CHANGED: Use DashboardDbContext instead of ApplicationDbContext
        private async Task<Customer> GetCurrentCustomerAsync(ApplicationUser currentUser)
        {
            var customer = await _context.Customers // CHANGED from _context
                .FirstOrDefaultAsync(c => c.IdentityUserId == currentUser.Id && !c.IsDeleted);

            return customer;
        }

        private string BuildCustomerAddress(Customer customer)
        {
            if (customer == null) return string.Empty;

            var addressParts = new List<string>();
            if (!string.IsNullOrEmpty(customer.StreetNumber)) addressParts.Add(customer.StreetNumber);
            if (!string.IsNullOrEmpty(customer.StreetName)) addressParts.Add(customer.StreetName);
            if (!string.IsNullOrEmpty(customer.Suburb)) addressParts.Add(customer.Suburb);
            if (!string.IsNullOrEmpty(customer.City)) addressParts.Add(customer.City);
            if (!string.IsNullOrEmpty(customer.PostalCode)) addressParts.Add(customer.PostalCode);

            return string.Join(", ", addressParts);
        }

        private byte[] GenerateInvoicePdf(Order order)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(50);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Column(headerColumn =>
                    {
                        headerColumn.Spacing(10);
                        headerColumn.Item().Row(headerRow =>
                        {
                            headerRow.RelativeItem().Column(companyColumn =>
                            {
                                companyColumn.Item().Text("Fridge Frenzy")
                                    .FontSize(28).Bold().FontColor(Colors.Blue.Darken4);
                                companyColumn.Item().PaddingTop(5).Text("Premium Refrigeration Services")
                                    .FontSize(13).SemiBold().FontColor(Colors.Blue.Darken2);
                                companyColumn.Item().PaddingTop(10).Text("123 Main Street")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                                companyColumn.Item().Text("Port Elizabeth, Eastern Cape, 6001")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                                companyColumn.Item().PaddingTop(3).Text("South Africa")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                                companyColumn.Item().PaddingTop(5).Text("Email: info@fridgefrenzy.com")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                                companyColumn.Item().Text("Phone: 081 028 6437")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                            });

                            headerRow.ConstantItem(180).Column(invoiceColumn =>
                            {
                                invoiceColumn.Item().Background(Colors.Blue.Darken4)
                                    .Padding(10).Column(invColumn =>
                                    {
                                        invColumn.Item().AlignCenter().Text("INVOICE")
                                            .FontSize(20).Bold().FontColor(Colors.White);
                                        invColumn.Item().PaddingTop(5).AlignCenter().Text($"#{order.Id.ToString().PadLeft(6, '0')}")
                                            .FontSize(14).SemiBold().FontColor(Colors.White);
                                    });

                                invoiceColumn.Item().PaddingTop(10).AlignRight().Text($"Date: {order.OrderDate:dd MMMM yyyy}")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                                invoiceColumn.Item().AlignRight().Text($"Time: {order.OrderDate:HH:mm}")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);

                                var statusColor = order.Status == "Pending" ? Colors.Orange.Darken2 :
                                                  order.Status == "Confirmed" ? Colors.Green.Darken2 :
                                                  order.Status == "Delivered" ? Colors.Green.Darken3 : Colors.Grey.Darken2;

                                invoiceColumn.Item().PaddingTop(5).AlignRight().Text($"Status: {order.Status}")
                                    .FontSize(9).Bold().FontColor(statusColor);
                            });
                        });

                        headerColumn.Item().PaddingTop(10).LineHorizontal(2).LineColor(Colors.Blue.Darken3);
                    });

                    page.Content().PaddingTop(20).Column(contentColumn =>
                    {
                        contentColumn.Spacing(20);
                        contentColumn.Item().Row(billingRow =>
                        {
                            billingRow.RelativeItem().Background(Colors.Grey.Lighten3)
                                .Padding(15).Column(billingColumn =>
                                {
                                    billingColumn.Item().Text("BILL TO")
                                        .FontSize(11).Bold().FontColor(Colors.Blue.Darken4);
                                    billingColumn.Item().PaddingTop(8).Text(order.CustomerName)
                                        .FontSize(11).SemiBold().FontColor(Colors.Black);
                                    billingColumn.Item().PaddingTop(3).Text(order.CustomerPhone)
                                        .FontSize(10).FontColor(Colors.Grey.Darken2);
                                    billingColumn.Item().PaddingTop(3).Text(order.DeliveryAddress)
                                        .FontSize(10).FontColor(Colors.Grey.Darken2);
                                });
                        });

                        contentColumn.Item().PaddingTop(10).Text("ORDER DETAILS")
                            .FontSize(12).Bold().FontColor(Colors.Blue.Darken4);

                        contentColumn.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(40);
                                columns.RelativeColumn(4);
                                columns.ConstantColumn(70);
                                columns.ConstantColumn(100);
                                columns.ConstantColumn(100);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Darken4).Padding(10)
                                    .Text("#").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken4).Padding(10)
                                    .Text("PRODUCT").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken4).Padding(10)
                                    .Text("QTY").FontColor(Colors.White).Bold().FontSize(9).AlignCenter();
                                header.Cell().Background(Colors.Blue.Darken4).Padding(10)
                                    .Text("UNIT PRICE").FontColor(Colors.White).Bold().FontSize(9).AlignRight();
                                header.Cell().Background(Colors.Blue.Darken4).Padding(10)
                                    .Text("TOTAL").FontColor(Colors.White).Bold().FontSize(9).AlignRight();
                            });

                            int itemNumber = 1;
                            bool alternate = false;

                            foreach (var item in order.OrderItems)
                            {
                                var bgColor = alternate ? Colors.Grey.Lighten4 : Colors.White;
                                alternate = !alternate;

                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(10).Text(itemNumber.ToString()).FontSize(9).FontColor(Colors.Grey.Darken1);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(10).Text(item.Product.Name).FontSize(10).FontColor(Colors.Black);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(10).Text(item.Quantity.ToString()).FontSize(10).AlignCenter().FontColor(Colors.Black);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(10).Text($"R {item.Price:N2}").FontSize(10).AlignRight().FontColor(Colors.Black);
                                table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(10).Text($"R {(item.Quantity * item.Price):N2}").FontSize(10).SemiBold().AlignRight().FontColor(Colors.Blue.Darken3);

                                itemNumber++;
                            }
                        });

                        contentColumn.Item().PaddingTop(20).AlignRight().Width(280).Column(totalsColumn =>
                        {
                            totalsColumn.Item().Table(totalsTable =>
                            {
                                totalsTable.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.ConstantColumn(120);
                                });

                                totalsTable.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                    .Text("Subtotal:").FontSize(10).FontColor(Colors.Grey.Darken2);
                                totalsTable.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                    .Text($"R {order.SubTotal:N2}").FontSize(10).AlignRight().FontColor(Colors.Black);

                                totalsTable.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                    .Text("Delivery Fee:").FontSize(10).FontColor(Colors.Grey.Darken2);
                                totalsTable.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                                    .Text($"R {order.DeliveryFee:N2}").FontSize(10).AlignRight().FontColor(Colors.Black);

                                totalsTable.Cell().Background(Colors.Blue.Darken4).Padding(10)
                                    .Text("TOTAL:").Bold().FontSize(12).FontColor(Colors.White);
                                totalsTable.Cell().Background(Colors.Blue.Darken4).Padding(10)
                                    .Text($"R {order.Total:N2}").Bold().FontSize(12).AlignRight().FontColor(Colors.White);
                            });
                        });

                        contentColumn.Item().PaddingTop(30).Background(Colors.Blue.Lighten4).Padding(15)
                            .Column(paymentColumn =>
                            {
                                paymentColumn.Item().Text("PAYMENT INFORMATION")
                                    .FontSize(10).Bold().FontColor(Colors.Blue.Darken4);
                                paymentColumn.Item().PaddingTop(8).Text("Thank you for your payment. Your order has been confirmed and will be processed shortly.")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);
                            });
                    });

                    page.Footer().Column(footerColumn =>
                    {
                        footerColumn.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        footerColumn.Item().PaddingTop(15).AlignCenter()
                            .Text("Thank you for choosing Fridge Frenzy!")
                            .FontSize(11).SemiBold().FontColor(Colors.Blue.Darken3);
                        footerColumn.Item().PaddingTop(5).AlignCenter()
                            .Text("For any inquiries, please contact us at info@fridgefrenzy.com or call 081 028 6437")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                        footerColumn.Item().PaddingTop(8).AlignCenter()
                            .Text("© 2025 Fridge Frenzy - Premium Refrigeration Services - All Rights Reserved")
                            .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                        footerColumn.Item().PaddingTop(10).AlignCenter().Text(text =>
                        {
                            text.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken2);
                            text.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken2);
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}