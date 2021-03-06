﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Web.Mvc;
using System.Text;
using System.Threading.Tasks;
using LIMS.MVCFoundation.Core;
using LIMS.MVCFoundation.Controllers;
using LIMS.Services;
using LIMS.Models;
using LIMS.Entities;
using LIMS.Models.Setting;
using LIMS.Util;
using LIMS.MVCFoundation.Attributes;

namespace LIMS.Web.Controllers.Setting
{
    [RequiredLogon]
    [BaseEntityValue]
    public class SystemFunctionController : BaseController
    {
        public ActionResult Index(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return View();
            }

            ViewBag.UnitId = unitId;
            ViewBag.Functions = new SystemFunctionService().GetAll();
            ViewBag.Privileges = new SystemPrivilegeService().GetByObjectId(unitId);

            return View();
        }
        /// <summary>
        /// 超级管理员管理权限
        /// </summary>
        /// <param name="unitId"></param>
        /// <returns></returns>
        [HttpPost]
        public JsonNetResult GetPrivilegesAdmin(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return JsonNet(new ResponseResult(false, "The unit id is empty."));
            }
            var functions = new SystemFunctionService().GetAll().ToList();
            List<object> alTree = new List<object>();
            var mainFunctions = new List<SystemFunctionEntity>();
            var userPri = new SystemPrivilegeService().GetByObjectId(unitId).ToList().Where(m => m.Operate);
            var funKeyList = new List<Funkey>
            {
                Funkey.User,
                Funkey.HospitalSetting,
                Funkey.HospitalSettingUnit,
                Funkey.Product,
                Funkey.VendorSettingUnit,
                Funkey.VendorSetting,
            };
            if (!this.IsAdmin)
            {
                var mainPrivileges = new SystemPrivilegeService().GetByObjectId(UserContext.UnitId).Where(m => m.Operate).ToList();//主单位的权限
                mainPrivileges.ForEach(m =>
                {
                    if (funKeyList.Any(j => j.ToString() == m.FunKey))
                    {
                        return;
                    }
                    if (functions.FirstOrDefault(j => j.FunKey == m.FunKey) != null)
                    {
                        mainFunctions.Add(functions.FirstOrDefault(j => j.FunKey == m.FunKey));
                    }

                });
                mainFunctions.AddRange(functions.Where(m => string.IsNullOrWhiteSpace(m.ParentId)));
            }
            else
            {
                mainFunctions = functions;
            }
            mainFunctions.Where(m => string.IsNullOrWhiteSpace(m.ParentId)).ToList().ForEach(m =>
            {
                List<SystemFunctionModel> childNode = mainFunctions.Where(j => j.ParentId == m.Id).Select(j => new SystemFunctionModel
                {
                    Id = j.Id,
                    Title = j.Title,
                    IsMenu = j.IsMenu,
                    FunKey = j.FunKey,
                    Url = j.Url,
                    ParentId = j.ParentId,
                    IsActive = j.IsActive,
                    Sequence = j.Sequence,
                    DisplayMode = j.DisplayMode,
                    SubFunctions = j.SubFunctions,
                    Operate = userPri.Any(w => w.FunKey == j.FunKey && w.Operate)
                }).ToList();
                if (childNode.Any())
                {
                    alTree.Add(new { parent = m, childNode });
                }
            });
            return JsonNet(new ResponseResult(true, new
            {
                UnitId = unitId,
                Functions = alTree
            }));
        }

        /// <summary>
        /// 超级管理员保存权限
        /// </summary>
        /// <param name="unitId"></param>
        /// <returns></returns>
        public JsonNetResult Save(string unitId, IList<PrivilegeItem> privileges)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return JsonNet(new ResponseResult(false, "The unit id is empty."));
            }

            var entities = new List<SystemPrivilegeEntity>();
            if (privileges != null)
            {
                entities = privileges.Select(item => new SystemPrivilegeEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    ObjectId = unitId,
                    ObjectType = 0,
                    FunKey = item.Id,
                    Query = item.Query,
                    Operate = item.Operate
                }).ToList();
            }
            new SystemPrivilegeService().Save(unitId, entities);
            return JsonNet(new ResponseResult());
        }


        public ActionResult UserPrivilege(string userId)
        {
            ViewBag.UserId = userId;
            ViewBag.Roots = GetRoots(userId);

            var user = new UserService().Get(userId);
            var unit = new UnitService().Get(user.UnitId);

            ViewBag.CurrentRootId = unit.RootId;

            return View();
        }

        private IDictionary<UnitType, List<object>> GetRoots(string userId)
        {
            var user = new UserService().Get(userId);
            var unitService = new UnitService();

            var unit = unitService.Get(user.UnitId);

            if (unit.Type == UnitType.None)
            {
                return null;
            }

            var dic = new Dictionary<UnitType, List<object>>();
            if (unit.Type == UnitType.Hospital || unit.Type == UnitType.HospitalUnit)
            {
                var roots = unitService.GetByRootId(Constant.DEFAULT_UNIT_ROOT_ID);

                dic[UnitType.Hospital] = roots.Where(item => item.Type == UnitType.Hospital).Select(item =>
                new
                {
                    Id = item.Id,
                    Name = item.Name
                }).ToList<object>();
            }
            else
            {
                var vendor = new UnitService().Get(unit.RootId == Constant.DEFAULT_UNIT_ROOT_ID ? unit.Id : unit.RootId);
                dic[UnitType.Vendor] = new List<object>()
                {
                    new
                    {
                        Id = vendor.Id,
                        Name = vendor.Name
                    }
                };
            }

            return dic;
        }

        public JsonNetResult SaveUserPrivilege(string userId, string rootId, IList<PrivilegeItem> privileges)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return JsonNet(new ResponseResult(false, "The user id is empty."));
            }

            if (string.IsNullOrEmpty(rootId))
            {
                return JsonNet(new ResponseResult(false, "The root id is empty."));
            }

            var entities = new List<UserPrivilegeEntity>();
            if (privileges != null)
            {
                entities = privileges.Select(item => new UserPrivilegeEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    UnitId = item.Id,
                    UnitRootId = rootId,
                    Query = item.Query,
                    Operate = item.Operate
                }).ToList();
            }

            new UserPrivilegeService().Save(userId, rootId, entities);

            return JsonNet(new ResponseResult());
        }

        public JsonNetResult GetUserPrivileges(string userId, string parentId)
        {
            var units = new UnitService().GetByRootId(parentId);
            var privileges = new UserPrivilegeService().Query(userId, parentId);

            return JsonNet(new ResponseResult(true, new { units = units, privileges = privileges }));
        }

        public JsonNetResult JsonGetUserPrivileges(string userId, string parentId)
        {
            var units = new UnitService().GetByRootId(parentId).ToList();
            var privileges = new UserPrivilegeService().Query(userId, parentId);
            var operateUnits = new List<UserUnitModel>();
            units.ForEach(m =>
            {
                operateUnits.Add(new UserUnitModel
                {
                    Id = m.Id,
                    Name = m.Name,
                    RootId = m.RootId,
                    Operate = privileges.Any(j => j.UnitId == m.Id && j.Operate)
                });
            });
            return JsonNet(new ResponseResult(true, new { units = operateUnits }));
        }
    }
}
