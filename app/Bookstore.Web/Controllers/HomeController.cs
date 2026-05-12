using Bookstore.Domain.Books;
using Bookstore.Web.ViewModel;
using Bookstore.Web.ViewModel.Home;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Bookstore.Web.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly IBookService bookService;

        public HomeController(IBookService bookService)
        {
            this.bookService = bookService;
        }

        public async Task<IActionResult> Index()
        {
            var books = await bookService.ListBestSellingBooksAsync(4);

            return View(new HomeIndexViewModel(books));
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Search()
        {
            return RedirectToAction("Index", "Search");
        }

        public IActionResult Cart()
        {
            return RedirectToAction("Index", "ShoppingCart");
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id });
        }
    }
}
