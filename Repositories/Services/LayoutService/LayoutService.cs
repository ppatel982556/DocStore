using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repositories.Interfaces;
using Repositories.Models.ViewModels;
using Repositories.Services.CloudinaryService;
using Repositories.Services.UserService;

namespace Repositories.Services.LayoutService
{
    public class LayoutService : ILayoutService
    {
        private readonly IUserService _userService;
        private readonly IRoleInterface _roleRepository;
        private readonly IPageInterface _pageRepository;
        private readonly ICloudinaryService _cloudinaryService;

        public LayoutService(
            IUserService userService,
            IRoleInterface roleRepository,
            IPageInterface pageRepository,
            ICloudinaryService cloudinaryService)
        {
            _userService = userService;
            _roleRepository = roleRepository;
            _pageRepository = pageRepository;
            _cloudinaryService = cloudinaryService;
        }
        public async Task<LayoutVM> GetLayoutAsync(int userId)
        {
            var user = await _userService.GetUserByIdAsync(userId);

            if (user == null)
                throw new Exception("User not found.");

            var roles = await _roleRepository.GetUserRolesAsync(userId);

            if (!roles.Any())
                throw new Exception("User has no roles assigned.");

            var activeRole = roles.FirstOrDefault(r =>
                r.RoleId == user.LastActiveRoleId);

            if (activeRole == null)
            {
                activeRole = roles.First();
            }

            var pages = await _pageRepository.GetPagesByRoleAsync(activeRole.RoleId);
                return new LayoutVM
                {
                    User = new CurrentUserVM
                    {
                        UserId = user.UserId,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        ProfileImageUrl = _cloudinaryService.GetProfilePictureUrl(user.ProfilePictureId),
                        ActiveRoleId = activeRole.RoleId,
                        ActiveRoleName = activeRole.RoleName
                    },

                    Roles = roles,

                    Pages = pages,

                    NotificationCount = 0
                };
        }
    }
}