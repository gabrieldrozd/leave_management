using AutoMapper;
using leave_management.Contracts;
using leave_management.Data;
using leave_management.Models;
using leave_management.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace leave_management.Controllers
{
    [Authorize]
    public class LeaveRequestController : Controller
    {
        private readonly ILeaveRequestRepository _leaveRequestRepo;
        private readonly ILeaveTypeRepository _leaveTypeRepo;
        private readonly ILeaveAllocationRepository _leaveAllocationRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IEmailSender _emailSender;
        private readonly UserManager<Employee> _userManager;

        public LeaveRequestController(
            ILeaveRequestRepository leaveRequestRepo,
            ILeaveTypeRepository leaveTypeRepo,
            ILeaveAllocationRepository leaveAllocationRepo,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IEmailSender emailSender,
            UserManager<Employee> userManager
        )
        {
            _leaveRequestRepo = leaveRequestRepo;
            _leaveTypeRepo = leaveTypeRepo;
            _leaveAllocationRepo = leaveAllocationRepo;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _emailSender = emailSender;
            _userManager = userManager;
        }

        [Authorize(Roles = "Administrator")]
        // GET: LeaveRequestController
        public async Task<ActionResult> Index()
        {
            //var leaveRequests = await _leaveRequestRepo.FindAll();
            var leaveRequests = await _unitOfWork.LeaveRequests.FindAll(
                orderBy: q => q.OrderByDescending(x => x.DateRequested),
                includes: q => q.Include(x => x.RequestingEmployee).Include(x => x.LeaveType));
            //var leaveRequestsOrdered = leaveRequests.OrderByDescending(x => x.DateRequested);
            var leaveRequestsModel = _mapper.Map<List<LeaveRequestViewModel>>(leaveRequests);
            var model = new AdminLeaveRequestViewViewModel
            {
                TotalRequests = leaveRequestsModel.Count,
                ApprovedRequests = leaveRequestsModel.Count(x => x.Approved == true),
                RejectedRequests = leaveRequestsModel.Count(x => x.Approved == false),
                PendingRequests = leaveRequestsModel.Count(x => x.Approved == null),
                LeaveRequests = leaveRequestsModel
            };
            return View(model);
        }

        [Authorize(Roles = "Employee")]
        public async Task<ActionResult> MyLeave()
        {
            var employee = await _userManager.GetUserAsync(User);
            var employeeId = employee.Id;

            //var employeeAllocations = await _leaveAllocationRepo.GetLeaveAllocationsByEmployee(employeeId);
            var employeeAllocations = await _unitOfWork.LeaveAllocations.FindAll(
                expression: x => x.EmployeeId == employeeId && x.Period == DateTime.Now.Year,
                includes: q => q.Include(x => x.LeaveType));

            //var employeeRequests = await _leaveRequestRepo.GetLeaveRequestsByEmployee(employeeId);
            var employeeRequests = await _unitOfWork.LeaveRequests.FindAll(
                expression: x => x.RequestingEmployeeId == employeeId, 
                orderBy: x => x.OrderByDescending(y => y.DateRequested)
                );

            //var employeeRequestsOrdered = employeeRequests.OrderByDescending(x => x.DateRequested);

            var employeeAllocationsModel = _mapper.Map<List<LeaveAllocationViewModel>>(employeeAllocations);
            var employeeRequestsModel = _mapper.Map<List<LeaveRequestViewModel>>(employeeRequests);

            var model = new EmployeeLeaveRequestViewViewModel
            {
                LeaveAllocations = employeeAllocationsModel,
                LeaveRequests = employeeRequestsModel
            };

            return View(model);
        }

        // GET: LeaveRequestController/Details/5
        public async Task<ActionResult> Details(int id)
        {
            //var leaveRequest = await _leaveRequestRepo.FindById(id);
            var leaveRequest = await _unitOfWork.LeaveRequests.Find(x => x.Id == id,
                includes: q => q.Include(x => x.ApprovedBy).Include(x => x.RequestingEmployee).Include(x => x.LeaveType));
            var model = _mapper.Map<LeaveRequestViewModel>(leaveRequest);
            return View(model);
        }

        public async Task<ActionResult> ApproveRequest(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                //var leaveRequest = await _leaveRequestRepo.FindById(id);
                var leaveRequest = await _unitOfWork.LeaveRequests.Find(x => x.Id == id,
                    includes: q => q.Include(x => x.ApprovedBy).Include(x => x.RequestingEmployee).Include(x => x.LeaveType));
                var employeeId = leaveRequest.RequestingEmployeeId;
                var leaveTypeId = leaveRequest.LeaveTypeId;

                //var allocation = await _leaveAllocationRepo.GetLeaveAllocationByEmployeeAndType(employeeId, leaveTypeId);
                var allocation = await _unitOfWork.LeaveAllocations.Find(
                    x => x.EmployeeId == employeeId && x.Period == DateTime.Now.Year && x.LeaveTypeId == leaveTypeId,
                    includes: q => q.Include(x => x.Employee).Include(x => x.LeaveType));
                int daysRequested = (int)(leaveRequest.EndDate.Date - leaveRequest.StartDate.Date).TotalDays;

                allocation.NumberOfDays -= daysRequested;
                leaveRequest.Approved = true;
                leaveRequest.ApprovedById = user.Id;
                leaveRequest.DateActioned = DateTime.Now;

                //await _leaveAllocationRepo.Update(allocation);
                //await _leaveRequestRepo.Update(leaveRequest);
                _unitOfWork.LeaveAllocations.Update(allocation);
                _unitOfWork.LeaveRequests.Update(leaveRequest);
                await _unitOfWork.Save();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                return RedirectToAction(nameof(Index), "Home");
            }
        }

        public async Task<ActionResult> RejectRequest(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                //var leaveRequest = await _leaveRequestRepo.FindById(id);
                var leaveRequest = await _unitOfWork.LeaveRequests.Find(
                    x => x.Id == id,
                    includes: q => q.Include(x => x.ApprovedBy).Include(x => x.RequestingEmployee).Include(x => x.LeaveType));
                leaveRequest.Approved = false;
                leaveRequest.ApprovedById = user.Id;
                leaveRequest.DateActioned = DateTime.Now;

                //await _leaveRequestRepo.Update(leaveRequest);
                _unitOfWork.LeaveRequests.Update(leaveRequest);
                await _unitOfWork.Save();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                return RedirectToAction(nameof(Index), "Home");
            }
        }

        // GET: LeaveRequestController/Create
        public async Task<ActionResult> Create()
        {
            //var leaveTypes = await _leaveTypeRepo.FindAll();
            var leaveTypes = await _unitOfWork.LeaveTypes.FindAll();
            var leaveTypeItems = leaveTypes.Select(q => new SelectListItem
            {
                Text = q.Name,
                Value = q.Id.ToString()
            });
            var model = new CreateLeaveRequestViewModel
            {
                LeaveTypes = leaveTypeItems
            };
            return View(model);
        }

        // POST: LeaveRequestController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(CreateLeaveRequestViewModel model)
        {
            try
            {
                var startDate = Convert.ToDateTime(model.StartDate);
                var endDate = Convert.ToDateTime(model.EndDate);
                //var leaveTypes = await _leaveTypeRepo.FindAll();
                var leaveTypes = await _unitOfWork.LeaveTypes.FindAll();
                var leaveTypeItems = leaveTypes.Select(q => new SelectListItem
                {
                    Text = q.Name,
                    Value = q.Id.ToString()
                });
                model.LeaveTypes = leaveTypeItems;

                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                if (DateTime.Compare(startDate, endDate) > 1)
                {
                    ModelState.AddModelError("", "Start Date can't be greater than End Date");
                    return View(model);
                }

                var employee = await _userManager.GetUserAsync(User);
                //var allocation = await _leaveAllocationRepo.GetLeaveAllocationByEmployeeAndType(employee.Id, model.LeaveTypeId);
                var allocation = await _unitOfWork.LeaveAllocations.Find(
                    x => x.EmployeeId == employee.Id && x.Period == DateTime.Now.Year && x.LeaveTypeId == model.LeaveTypeId,
                    includes: q => q.Include(x => x.Employee).Include(x => x.LeaveType));
                int daysRequested = (int)(endDate.Date - startDate.Date).TotalDays;

                if (daysRequested > allocation.NumberOfDays)
                {
                    ModelState.AddModelError("", "You don't have sufficient days for this request");
                    return View(model);
                }

                var leaveRequestModel = new LeaveRequestViewModel
                {
                    RequestingEmployeeId = employee.Id,
                    LeaveTypeId = model.LeaveTypeId,
                    StartDate = startDate,
                    EndDate = endDate,
                    Approved = null,
                    DateRequested = DateTime.Now,
                    DateActioned = DateTime.Now,
                    RequestComments = model.RequestComments
                };
                var leaveRequest = _mapper.Map<LeaveRequest>(leaveRequestModel);

                //var isSuccess = await _leaveRequestRepo.Create(leaveRequest);
                await _unitOfWork.LeaveRequests.Create(leaveRequest);
                await _unitOfWork.Save();

                //await _emailSender.SendEmailAsync("emailAdmina@gmail.com", "New Leave Request", 
                //    $"Please review this leave request. <a href='UrlOfApp/{leaveRequest.Id}'>Click here</a>.");

                return RedirectToAction(nameof(MyLeave));
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "Something went wrong...");
                return View(model);
            }
        }

        public async Task<ActionResult> CancelRequest(int id)
        {
            //var leaveRequest = await _leaveRequestRepo.FindById(id);
            var leaveRequest = await _unitOfWork.LeaveRequests.Find(
                x => x.Id == id,
                includes: q => q.Include(x => x.ApprovedBy).Include(x => x.RequestingEmployee).Include(x => x.LeaveType));
            leaveRequest.Cancelled = true;
            //await _leaveRequestRepo.Update(leaveRequest);
            _unitOfWork.LeaveRequests.Update(leaveRequest);
            await _unitOfWork.Save();
            return RedirectToAction("MyLeave");
        }

        // GET: LeaveRequestController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: LeaveRequestController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
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

        // GET: LeaveRequestController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: LeaveRequestController/Delete/5
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
