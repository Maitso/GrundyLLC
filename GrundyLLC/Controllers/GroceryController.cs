using GrundyLLC.Models;
using IpStack.Net.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Reflection;
// Controllers/GroceryController.cs
using System.Text;
using static PaystackApi.Core.WebHook.WebHookEvents;

namespace GrundyLLC.Controllers
{
    public class GroceryController : Controller
    {
        private static readonly HttpClient client = new HttpClient();
        private const string PaystackSecretKey = "sk_test_91a442b9401cea20112fd5440e0e0b5a1523890b";
        private const string PaystackPublicKey = "pk_test_1b5dab4195ca2663a6c4de020ce3a912167cedc6";

        // Simulate a product catalog
        private static readonly List<Product> Products = new List<Product>
        {
            new Product { Id = 1, Name = "Apple", Price = 20.5M, ImageUrl = "/images/products/apple.jpg" },
            new Product { Id = 2, Name = "Banana", Price = 30.0M, ImageUrl = "/images/products/banana.jpg" },
            new Product { Id = 3, Name = "Carrot", Price = 20.0M, ImageUrl = "/images/products/carrot.jpg" },
            new Product { Id = 4, Name = "Dates", Price = 15.5M, ImageUrl = "/images/products/dates.jpg" },
            new Product { Id = 5, Name = "Edamame Beans", Price = 10.0M, ImageUrl = "/images/products/edamame.jpg" },
            new Product { Id = 6, Name = "Fig", Price = 25.0M, ImageUrl = "/images/products/fig.jpg" },
            new Product { Id = 7, Name = "Grapes", Price = 25.0M, ImageUrl = "/images/products/grapes.jpg" },
            new Product { Id = 8, Name = "Honey Dew Melon", Price = 25.0M, ImageUrl = "/images/products/honeydewmelon.jpg" },
        };


        // Simulate the user's cart
        private static List<Product> Cart = new List<Product>();

        //constructor for auth
        static GroceryController()
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {PaystackSecretKey}");
        }

        // Simulated database or repository for subscription options
        private static readonly Models.Subscription WeeklySubscription = new Models.Subscription
        {
            SubscriptionType = "Weekly",
            Price = 50.00m
        };

        private static readonly Models.Subscription MonthlySubscription = new Models.Subscription
        {
            SubscriptionType = "Monthly",
            Price = 200.00m
        };

        //Display all items
        public IActionResult Index()
        {
            return View(Products);
        }

        public IActionResult ShopView() =>
            // Your logic to return the products view or data
            View(Products);

        //Adding items to cart
        [HttpPost]
        public IActionResult AddToCart(int productId)
        {
            var product = Products.FirstOrDefault(p => p.Id == productId);
            if (product != null)
            {
                Cart.Add(product);
            }
            return RedirectToAction("CartView");
        }

        //Display all items in cart
        public async Task<IActionResult> CartViewAsync()
        {
            // Set the current subscription in TempData to display it in CartView and Checkout
            var SubscriptionDetails = await FindSubscriptionById(); //Check if user has a valid subscription

            if (SubscriptionDetails.status == "True")
            {
                TempData["SubscriptionDetails"] = SubscriptionDetails.message;
                //TempData["SubscriptionDetails"] = SubscriptionDetails; // Object with subscription details
            }
            else
            {
                await CreateSubscriptions(); // if the user does not have a valid subscription let's create a new one.
            }
            return View(Cart);
        }
        
        // Handle the subscription
        [HttpPost]
        public IActionResult AddSubscription(string subscriptionType)
        {
            Models.Subscription subscription = null;

            // Check which subscription was selected
            if (subscriptionType == "Weekly")
            {
                subscription = new Models.Subscription { SubscriptionType = "Weekly", Price = 50M };
            }
            else if (subscriptionType == "Monthly")
            {
                subscription = new Models.Subscription { SubscriptionType = "Monthly", Price = 200M };
            }


            // Save the selected subscription to TempData (or to a database)
            TempData["Subscription"] = subscription;

            // Redirect back to the CartView with the added subscription
            return RedirectToAction("CartView");
        }

        [HttpPost]
        public async Task<IActionResult> Checkout()
        {
            var amountInRands = Cart.Sum(item => item.Price); //Convert to Cents (1 Rand = 100 Cents)
            int amountInCents = (int)(amountInRands * 100);

            // Calculate the total with subscription and platform fee
            decimal totalPrice = Cart.Sum(p => p.Price); // Sum of all product prices
            decimal subscriptionPrice = (TempData["Subscription"] != null ?
                ((Models.Subscription)TempData["Subscription"]).Price : 0);  // Get subscription price if available, otherwise 0
            decimal platformFee = (totalPrice + subscriptionPrice) * 0.125M;  // 12.5% platform fee

            decimal amountPayment = (totalPrice + platformFee + subscriptionPrice)*100;
            //double grandTotal = amountInCents + subscriptionFee + platformFee;

            //Create a Paystack transaction

            var paymentUrl = await CreatePaystackTransaction(amountPayment);

            if (paymentUrl != null)
            {
                return Redirect(paymentUrl);
            }
            else
            {
                return View("Error");
            }
        }

        private async Task<string> CreatePaystackTransaction(decimal amount)
        {
            var requestBody = new
            {
                // Transaction details
                email = "customer@example.com", 
                amount = amount,
                Currency = "ZAR",
                callback_url = "https://grundyllc-production.up.railway.app/grocery/paymentcallback"
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.paystack.co/transaction/initialize", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var responseJson = JsonConvert.DeserializeObject<dynamic>(responseBody);
                return responseJson.data.authorization_url;
            }


                return null;
        }

        private async Task<string> CreateSubscriptions()
        {
            var requestBody = new
            {
                // Subscription details
                customer = "customer@example.com",
                plan = "PLN_gfgqd8iq69rdyk9",
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.paystack.co/subscription", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var responseJson = JsonConvert.DeserializeObject<dynamic>(responseBody);
                return responseJson.status;
            }

            return null;
        }

        private async Task<dynamic> FindSubscriptionById()
        {
            //We would find this dynamically
            var subscriptionID = 775900;
            //client.DefaultRequestHeaders.Add("Authorization", $"Bearer {PaystackSecretKey}");
            var response = await client.GetAsync($"https://api.paystack.co/subscription/775900");
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                
                var responseJson = JsonConvert.DeserializeObject<dynamic>(responseBody);
                return responseJson;
            }
            else
            {
                Console.WriteLine(response);
            }

            return null;
        }

        [HttpGet]
        public IActionResult PaymentCallback(string reference)
        {
            // Verify payment with Paystack
            return View();
        }
    }
}
