using System;
using System.Threading.Tasks;
using CmdNext.Models.Domain.Model.App.Admin;

namespace CmdNext.Service.Contracts
{
    public interface IUserService
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User> CreateAsync(User user, string password);
        Task<bool> ValidatePasswordAsync(User user, string password);
        Task<User?> GetByIdAsync(Guid userId);
    }
}
