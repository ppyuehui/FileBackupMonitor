using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoUpdater;

namespace FileBackupMonitor
{
    public class UpdateManager
    {
        private readonly UpdateService _giteaUpdater;
        private readonly UpdateService _giteeUpdater;

        // ==================== Gitea 配置（主源） ====================
        private const string GITEA_SERVER_URL = "https://git.huihui.xx.kg:2222";
        private const string GITEA_REPO_OWNER = "gitea";
        private const string GITEA_REPO_NAME = "FileBackupMonitor";
        private const string GITEA_ACCESS_TOKEN = "03e89c14ded2b8d2207443319f13f92bb3a1afdc";
        private const string GITEA_API_VERSION = "v1";
        // ===========================================================

        // ==================== Gitee 配置（备用源） ====================
        private const string GITEE_REPO_OWNER = "ppyuehui";
        private const string GITEE_REPO_NAME = "FileBackupMonitor";
        private const string GITEE_ACCESS_TOKEN = "4a63da8fe4818e1889fa263289202d8f";
        // ===========================================================

        // ==================== 通用配置 ====================
        /// <summary>当前版本号（每次发布新版本时手动更新，格式 YY.MM.DD）</summary>
        public const string APP_VERSION = "26.06.02";
        // =================================================

        /// <summary>获取当前版本号</summary>
        public string CurrentVersion => APP_VERSION;

        public UpdateManager()
        {
            // Gitea 主源
            var giteaConfig = new UpdateConfig
            {
                ServerUrl = GITEA_SERVER_URL,
                RepoOwner = GITEA_REPO_OWNER,
                RepoName = GITEA_REPO_NAME,
                CurrentVersion = APP_VERSION,
                AccessToken = GITEA_ACCESS_TOKEN,
                ApiVersion = GITEA_API_VERSION
            };
            _giteaUpdater = new UpdateService(giteaConfig);

            // Gitee 备用源
            var giteeConfig = UpdateConfig.CreateGitee(
                GITEE_REPO_OWNER,
                GITEE_REPO_NAME,
                APP_VERSION,
                GITEE_ACCESS_TOKEN
            );
            _giteeUpdater = new UpdateService(giteeConfig);
        }

        /// <summary>
        /// 检查是否有新版本（每天只检查一次）
        /// 优先使用 Gitea，失败时自动切换到 Gitee
        /// </summary>
        /// <returns>返回更新信息，没有更新返回 null</returns>
        public async Task<UpdateInfo> CheckForUpdateAsync(bool forceCheck = false)
        {
            // 强制检查时跳过每日限制
            if (!forceCheck && _giteaUpdater.HasCheckedToday())
                return null;

            try
            {
                // 优先尝试 Gitea
                var result = await _giteaUpdater.CheckForUpdateAsync();
                if (result != null)
                    return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Gitea 检查失败: {ex.Message}，尝试 Gitee...");
            }

            try
            {
                // Gitea 失败或无更新，尝试 Gitee
                var result = await _giteeUpdater.CheckForUpdateAsync();
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Gitee 检查也失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 下载并应用更新
        /// </summary>
        /// <param name="updateInfo">更新信息</param>
        /// <param name="progressCallback">进度回调 (0-100)</param>
        public async Task<bool> DownloadAndUpdateAsync(UpdateInfo updateInfo, Action<int> progressCallback = null)
        {
            // 根据下载链接判断使用哪个源
            bool isGitea = updateInfo.DownloadUrl?.Contains(GITEA_SERVER_URL) ?? false;
            var updater = isGitea ? _giteaUpdater : _giteeUpdater;

            return await updater.DownloadAndUpdateAsync(updateInfo, progressCallback, () =>
            {
                // WPF 应用退出
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    System.Windows.Application.Current.Shutdown());
            });
        }
    }
}
