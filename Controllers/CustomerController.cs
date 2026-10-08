using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Policy = "SalesAccess")]
public class CustomerController : Controller
{
    private readonly ApplicationDbContext _context;

    public CustomerController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(Customer customer)
    {
        customer.CustomerCode = $"CUS-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        customer.CreatedDate = DateTime.UtcNow;
        customer.CreatedBy = User.Identity?.Name ?? "System";
        customer.Status = "Active";

        ModelState.Remove(nameof(Customer.CustomerCode));
        ModelState.Remove(nameof(Customer.CreatedDate));
        ModelState.Remove(nameof(Customer.CreatedBy));
        ModelState.Remove(nameof(Customer.Status));

        if (!ModelState.IsValid)
        {
            return View(customer);
        }

        var emailExists = await _context.Customers
            .AnyAsync(c => c.Email == customer.Email);

        if (emailExists)
        {
            ModelState.AddModelError(nameof(Customer.Email),
                "A customer with this email already exists.");
            return View(customer);
        }

        var phoneExists = await _context.Customers
            .AnyAsync(c => c.Phone == customer.Phone);

        if (phoneExists)
        {
            ModelState.AddModelError(nameof(Customer.Phone),
                "A customer with this phone number already exists.");
            return View(customer);
        }

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm)
    {
        IQueryable<Customer> customerQuery = _context.Customers;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchPattern = $"%{searchTerm.Trim()}%";
            customerQuery = customerQuery.Where(customer =>
                EF.Functions.ILike(customer.CustomerName, searchPattern) ||
                EF.Functions.ILike(customer.Email, searchPattern) ||
                EF.Functions.ILike(customer.Phone, searchPattern) ||
                (customer.CompanyName != null && EF.Functions.ILike(customer.CompanyName, searchPattern)) ||
                EF.Functions.ILike(customer.CustomerCode, searchPattern));
        }

        var customers = await customerQuery
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync();

        ViewData["SearchTerm"] = searchTerm;
        return View(customers);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer is null)
        {
            return NotFound();
        }

        return View(customer);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer is null)
        {
            return NotFound();
        }

        return View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Customer customer)
    {
        var existingCustomer = await _context.Customers.FindAsync(customer.CustomerId);
        if (existingCustomer is null)
        {
            return NotFound();
        }

        customer.CustomerCode = existingCustomer.CustomerCode;
        customer.CreatedDate = existingCustomer.CreatedDate;
        customer.CreatedBy = existingCustomer.CreatedBy;

        ModelState.Remove(nameof(Customer.CustomerCode));
        ModelState.Remove(nameof(Customer.CreatedDate));
        ModelState.Remove(nameof(Customer.CreatedBy));

        if (!ModelState.IsValid)
        {
            return View(customer);
        }

        var emailExists = await _context.Customers
            .AnyAsync(c => c.CustomerId != customer.CustomerId && c.Email == customer.Email);
        if (emailExists)
        {
            ModelState.AddModelError(nameof(Customer.Email),
                "A customer with this email already exists.");
            return View(customer);
        }

        var phoneExists = await _context.Customers
            .AnyAsync(c => c.CustomerId != customer.CustomerId && c.Phone == customer.Phone);
        if (phoneExists)
        {
            ModelState.AddModelError(nameof(Customer.Phone),
                "A customer with this phone number already exists.");
            return View(customer);
        }

        existingCustomer.CustomerName = customer.CustomerName;
        existingCustomer.Email = customer.Email;
        existingCustomer.Phone = customer.Phone;
        existingCustomer.CompanyName = customer.CompanyName;
        existingCustomer.Address = customer.Address;
        existingCustomer.City = customer.City;
        existingCustomer.State = customer.State;
        existingCustomer.Status = customer.Status;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer is null)
        {
            return NotFound();
        }

        return View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer is null)
        {
            return NotFound();
        }

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}