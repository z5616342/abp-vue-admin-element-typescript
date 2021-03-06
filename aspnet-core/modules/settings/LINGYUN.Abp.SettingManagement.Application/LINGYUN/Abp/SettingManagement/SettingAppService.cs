﻿using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.SettingManagement;
using Volo.Abp.SettingManagement.Localization;
using Volo.Abp.Settings;

namespace LINGYUN.Abp.SettingManagement
{
    [Authorize(AbpSettingManagementPermissions.Settings.Default)]
    public class SettingAppService : ApplicationService, ISettingAppService
    {
        protected ISettingManager SettingManager { get; }
        protected ISettingDefinitionManager SettingDefinitionManager { get; }

        protected IDistributedCache<SettingCacheItem> Cache { get; }
        public SettingAppService(
            ISettingManager settingManager,
            IDistributedCache<SettingCacheItem> cache,
            ISettingDefinitionManager settingDefinitionManager)
        {
            Cache = cache;
            SettingManager = settingManager;
            SettingDefinitionManager = settingDefinitionManager;
            LocalizationResource = typeof(AbpSettingManagementResource);
        }

        public virtual async Task<ListResultDto<SettingDto>> GetAsync([NotNull] string providerName, [NotNull] string providerKey)
        {
            return await GetAllSettingAsync(providerName, providerKey);
        }

        [Authorize(AbpSettingManagementPermissions.Settings.Manager)]
        public virtual async Task UpdateAsync([NotNull] string providerName, [NotNull] string providerKey, UpdateSettingsDto input)
        {
            foreach (var setting in input.Settings)
            {
                await SettingManager.SetAsync(setting.Name, setting.Value, providerName, providerKey);
                // 同步变更缓存配置
                var settingCacheKey = CalculateCacheKey(setting.Name, providerName, providerKey);
                var settignCacheItem = new SettingCacheItem(setting.Value);
                await Cache.SetAsync(settingCacheKey, settignCacheItem);
            }
        }

        [AllowAnonymous]
        public virtual async Task<ListResultDto<SettingDto>> GetAllGlobalAsync()
        {
            var globalSettings = await SettingManager.GetAllGlobalAsync();

            return GetAllSetting(globalSettings);
        }

        public virtual async Task<ListResultDto<SettingDto>> GetAllForTenantAsync()
        {
            if (CurrentTenant.IsAvailable)
            {
                var tenantSettings = await SettingManager.GetAllForTenantAsync(CurrentTenant.Id.Value);
                return GetAllSetting(tenantSettings);
            }
            return new ListResultDto<SettingDto>();
        }

        public virtual async Task<ListResultDto<SettingDto>> GetAllForUserAsync([Required] Guid userId)
        {
            var userSettings = await SettingManager.GetAllForUserAsync(userId);
            return GetAllSetting(userSettings);
        }

        public virtual async Task<ListResultDto<SettingDto>> GetAllForCurrentUserAsync()
        {
            var userSettings = await SettingManager.GetAllForUserAsync(CurrentUser.Id.Value);
            return GetAllSetting(userSettings);
        }

        protected virtual async Task<ListResultDto<SettingDto>> GetAllSettingAsync(
            string providerName, string providerKey)
        {
            var settingsDto = new List<SettingDto>();
            var settingDefinitions = SettingDefinitionManager.GetAll();
            foreach (var setting in settingDefinitions)
            {
                if (setting.Providers.Any() && !setting.Providers.Contains(providerName))
                {
                    continue;
                }

                if (!setting.IsVisibleToClients)
                {
                    continue;
                }

                var settingValue = await SettingManager.GetOrNullAsync(setting.Name, providerName, providerKey);
                var settingInfo = new SettingDto
                {
                    Name = setting.Name,
                    Value = settingValue,
                    DefaultValue = setting.DefaultValue,
                    Description = setting.Description.Localize(StringLocalizerFactory),
                    DisplayName = setting.DisplayName.Localize(StringLocalizerFactory)
                };
                settingsDto.Add(settingInfo);
            }
            return new ListResultDto<SettingDto>(settingsDto);
        }

        protected virtual ListResultDto<SettingDto> GetAllSetting(
            List<SettingValue> settings)
        {
            var settingsDto = new List<SettingDto>();

            foreach (var setting in settings)
            {
                var settingDefinition = SettingDefinitionManager.Get(setting.Name);

                if (!settingDefinition.IsVisibleToClients)
                {
                    continue;
                }
                var settingInfo = new SettingDto
                {
                    Name = setting.Name,
                    Value = setting.Value,
                    DefaultValue = settingDefinition.DefaultValue,
                    Description = settingDefinition.Description.Localize(StringLocalizerFactory),
                    DisplayName = settingDefinition.DisplayName.Localize(StringLocalizerFactory)
                };
                settingsDto.Add(settingInfo);
            }
            return new ListResultDto<SettingDto>(settingsDto);
        }

        protected virtual string CalculateCacheKey(string name, string providerName, string providerKey)
        {
            return SettingCacheItem.CalculateCacheKey(name, providerName, providerKey);
        }
    }
}
