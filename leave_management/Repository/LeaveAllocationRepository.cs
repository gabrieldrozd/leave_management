using leave_management.Contracts;
using leave_management.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace leave_management.Repository
{
    public class LeaveAllocationRepository : ILeaveAllocationRepository
    {
        private readonly ApplicationDbContext _db;

        public LeaveAllocationRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<bool> CheckAllocation(int leaveTypeId, string employeeId)
        {
            var period = DateTime.Now.Year;
            var leaveAllocations = await FindAll();
            return leaveAllocations
                .Where(x => x.EmployeeId == employeeId && x.LeaveTypeId == leaveTypeId && x.Period == period)
                .Any();
        }

        public async Task<bool> Create(LeaveAllocation entity)
        {
            await _db.LeaveAllocations.AddAsync(entity);
            return await Save();
        }

        public async Task<bool> Delete(LeaveAllocation entity)
        {
            _db.LeaveAllocations.Remove(entity);
            return await Save();
        }

        public async Task<bool> DoExist(int id)
        {
            var exists = await _db.LeaveAllocations.AnyAsync(x => x.Id == id);
            return exists;
        }

        public async Task<ICollection<LeaveAllocation>> FindAll()
        {
            var leaveAllocations = await _db.LeaveAllocations
                .Include(x => x.LeaveType)
                .Include(y => y.Employee)
                .ToListAsync();
            return leaveAllocations;
        }

        public async Task<LeaveAllocation> FindById(int id)
        {
            var leaveAllocation = await _db.LeaveAllocations
                .Include(x => x.LeaveType)
                .Include(y => y.Employee)
                .FirstOrDefaultAsync(z => z.Id == id);
            return leaveAllocation;
        }

        public async Task<ICollection<LeaveAllocation>> GetLeaveAllocationsByEmployee(string id)
        {
            var period = DateTime.Now.Year;
            var leaveAllocations = await FindAll();
            return leaveAllocations
                .Where(x => x.EmployeeId == id && x.Period == period)
                .ToList();
        }

        public async Task<LeaveAllocation> GetLeaveAllocationByEmployeeAndType(string employeeId, int leaveTypeId)
        {
            var period = DateTime.Now.Year;
            var leaveAllocations = await FindAll();
            return leaveAllocations
                .FirstOrDefault(x => x.EmployeeId == employeeId && x.Period == period && x.LeaveTypeId == leaveTypeId);
        }

        public async Task<bool> Save()
        {
            var changes = await _db.SaveChangesAsync();
            return changes > 0;
        }

        public async Task<bool> Update(LeaveAllocation entity)
        {
            _db.LeaveAllocations.Update(entity);
            return await Save();
        }
    }
}
