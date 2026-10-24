/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace FufuLauncher.Contracts.Services
{
    public interface IUpdateService
    {
        Task<UpdateCheckResult> CheckUpdateAsync();

        Task<UpdateProbeResult> ProbeUpdateAsync(CancellationToken cancellationToken = default);

        Task<string> GetUpdateAnnouncementUrlAsync();
    }

    public class UpdateCheckResult
    {
        public bool ShouldShowUpdate
        {
            get;
            set;
        }

        public bool IsPreview
        {
            get;
            set;
        }

        public bool IsDevBuild
        {
            get;
            set;
        }

        public string ServerVersion
        {
            get;
            set;
        } = string.Empty;

        public string UpdateInfoUrl
        {
            get;
            set;
        } = string.Empty;
    }

    public class UpdateProbeResult
    {
        public bool Succeeded
        {
            get;
            set;
        }

        public string CurrentVersion
        {
            get;
            set;
        } = string.Empty;

        public string ServerVersion
        {
            get;
            set;
        } = string.Empty;

        public string PreviewVersion
        {
            get;
            set;
        } = string.Empty;

        public bool HasUpdate
        {
            get;
            set;
        }

        public bool HasPreviewUpdate
        {
            get;
            set;
        }

        public bool IsDevBuild
        {
            get;
            set;
        }

        public bool IsPreviewBuild
        {
            get;
            set;
        }

        public string UpdateInfoUrl
        {
            get;
            set;
        } = string.Empty;

        public string PreviewUpdateInfoUrl
        {
            get;
            set;
        } = string.Empty;

        public string Message
        {
            get;
            set;
        } = string.Empty;
    }
}