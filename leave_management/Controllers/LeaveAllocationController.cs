using AutoMapper;
using leave_management.Contracts;
using leave_management.Data;
using leave_management.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace leave_management.Controllers
{
    [Authorize(Roles = "Administrator")] // Need to log in before get there!
    public class LeaveAllocationController : Controller
    {
        private readonly ILeaveTypeRepository _leaveTypeRepo;
        private readonly ILeaveAllocationRepository _leaveAllocationRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly UserManager<Employee> _userManager; 

        public LeaveAllocationController(
            ILeaveTypeRepository leaveTypeRepo,
            ILeaveAllocationRepository leaveAllocationRepo,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            UserManager<Employee> userManager
        )
        {
            _leaveTypeRepo = leaveTypeRepo;
            _leaveAllocationRepo = leaveAllocationRepo;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _userManager = userManager;
        }

        // GET: LeaveAllocationController
        public async Task<ActionResult> Index()
        {
            //var leaveTypes = await _leaveTypeRepo.FindAll();
            var leaveTypes = await _unitOfWork.LeaveTypes.FindAll();
            var mappedLeaveTypes = _mapper.Map<List<LeaveType>, List<LeaveTypeViewModel>>(leaveTypes.ToList());
            var model = new CreateLeaveAllocationViewModel
            {
                LeaveTypes = mappedLeaveTypes,
                NumberUpdated = 0
            };
            return View(model);
        }

        public async Task<ActionResult> SetLeave(int id)
        {
            //var leaveType = await _leaveTypeRepo.FindById(id);
            var leaveType = await _unitOfWork.LeaveTypes.Find(
                x => x.Id == id);
            var employees = await _userManager.GetUsersInRoleAsync("Employee");

            foreach (var emp in employees)
            {
                //if (await _leaveAllocationRepo.CheckAllocation(id, emp.Id))
                //    continue;
                var allocationsWithConditions = await _unitOfWork.LeaveAllocations.FindAll(
                    x => x.EmployeeId == emp.Id && x.LeaveTypeId == leaveType.Id && x.Period == DateTime.Now.Year,
                    //includes: new List<string> { "Employee", "LeaveType" });
                    includes: x => x.Include(x => x.Employee).Include(x => x.LeaveType));
                if (allocationsWithConditions.Any())
                    continue;

                var allocation = new LeaveAllocationViewModel
                {
                    DateCreated = DateTime.Now,
                    EmployeeId = emp.Id,
                    LeaveTypeId = id,
                    NumberOfDays = leaveType.DefaultDays,
                    Period = DateTime.Now.Year

                };

                var leaveAllocation = _mapper.Map<LeaveAllocation>(allocation);
                //await _leaveAllocationRepo.Create(leaveAllocation);
                await _unitOfWork.LeaveAllocations.Create(leaveAllocation);
                await _unitOfWork.Save();
                
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<ActionResult> ListEmployees()
        {
            var employees = await _userManager.GetUsersInRoleAsync("Employee");
            var model = _mapper.Map<List<EmployeeViewModel>>(employees);
            return View(model);
        }

        // GET: LeaveAllocationController/Details/5
        public async Task<ActionResult> Details(string id)
        {
            //var employee = _mapper.Map<EmployeeViewModel>(_userManager.FindByIdAsync(id).Result);
            var employee = await _userManager.FindByIdAsync(id);
            var mappedEmployee = _mapper.Map<EmployeeViewModel>(employee);

            //var allocations = await _leaveAllocationRepo.GetLeaveAllocationsByEmployee(id);
            var allocations = await _unitOfWork.LeaveAllocations.FindAll(
                x => x.EmployeeId == id && x.Period == DateTime.Now.Year,
                includes: x => x.Include(x => x.Employee).Include(x => x.LeaveType));
            var mappedAllocations = _mapper.Map<List<LeaveAllocationViewModel>>(allocations);

            var model = new ViewAllocationsViewModel
            {
                Employee = mappedEmployee,
                LeaveAllocations = mappedAllocations
            };

            return View(model);
        }

        // GET: LeaveAllocationController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: LeaveAllocationController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: LeaveAllocationController/Edit/5
        public async Task<ActionResult> Edit(int id)
        {
            //var leaveAllocation = await _leaveAllocationRepo.FindById(id);
            var leaveAllocation = await _unitOfWork.LeaveAllocations.Find(
                x => x.Id == id,
                includes: x => x.Include(x => x.Employee).Include(x => x.LeaveType));
            var model = _mapper.Map<EditLeaveAllocationViewModel>(leaveAllocation);
            return View(model);
        }

        // POST: LeaveAllocationController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(EditLeaveAllocationViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    //var leaveAllocation = await _leaveAllocationRepo.FindById(model.Id);
                    var leaveAllocation = await _unitOfWork.LeaveAllocations.Find(
                        x => x.Id == model.Id,
                        includes: x => x.Include(x => x.Employee).Include(x => x.LeaveType));
                    var newModel = _mapper.Map<EditLeaveAllocationViewModel>(leaveAllocation);
                    return View(newModel);
                }
                //var record = await _leaveAllocationRepo.FindById(model.Id);
                var record = await _unitOfWork.LeaveAllocations.Find(
                    x => x.Id == model.Id,
                    includes: x => x.Include(x => x.Employee).Include(x => x.LeaveType));
                record.NumberOfDays = model.NumberOfDays;

                //var isSuccess = await _leaveAllocationRepo.Update(record);
                _unitOfWork.LeaveAllocations.Update(record);
                await _unitOfWork.Save();

                //if (!isSuccess)
                //{
                //    ModelState.AddModelError("", "Something went wrong...");
                //    return View(model);
                //}

                return RedirectToAction(nameof(Details), new { id = model.EmployeeId });
            }
            catch
            {
                return View(model);
            }
        }

        // GET: LeaveAllocationController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: LeaveAllocationController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
