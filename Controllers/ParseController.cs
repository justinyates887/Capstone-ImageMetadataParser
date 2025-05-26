using Microsoft.AspNetCore.Mvc;

namespace ImageMetadataParser.Controllers
{
    public class ParseController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
