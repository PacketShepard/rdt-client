﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RdtClient.Data.Models.Data;
using RdtClient.Service.Helpers;
using RdtClient.Service.Services;

namespace RdtClient.Web.Controllers
{
    [Authorize]
    [Route("Api/Torrents")]
    public class TorrentsController : Controller
    {
        private readonly ITorrents _torrents;
        private readonly IDownloads _downloads;
        private readonly ITorrentRunner _torrentRunner;

        public TorrentsController(ITorrents torrents, IDownloads downloads, ITorrentRunner torrentRunner)
        {
            _torrents = torrents;
            _downloads = downloads;
            _torrentRunner = torrentRunner;
        }

        [HttpGet]
        [Route("")]
        public async Task<ActionResult<IList<Torrent>>> Get()
        {
            var results = await _torrents.Get();
            
            // Prevent infinite recursion when serializing
            foreach (var file in results.SelectMany(torrent => torrent.Downloads))
            {
                file.Torrent = null;
            }
            
            return Ok(results);
        }

        /// <summary>
        /// Used for debugging only. Force a tick.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("Tick")]
        public async Task<ActionResult> Tick()
        {
            await _torrentRunner.Tick();

            return Ok();
        }

        [HttpPost]
        [Route("UploadFile")]
        public async Task<ActionResult> UploadFile([FromForm] IFormFile file,
                                                   [ModelBinder(BinderType = typeof(JsonModelBinder))]
                                                   TorrentControllerUploadFileRequest formData)
        {
            if (file == null || file.Length <= 0)
            {
                throw new Exception("Invalid torrent file");
            }

            var fileStream = file.OpenReadStream();

            await using var memoryStream = new MemoryStream();

            await fileStream.CopyToAsync(memoryStream);

            var bytes = memoryStream.ToArray();

            await _torrents.UploadFile(bytes, null, formData.AutoDownload, formData.AutoUnpack, formData.AutoDelete);

            return Ok();
        }

        [HttpPost]
        [Route("UploadMagnet")]
        public async Task<ActionResult> UploadMagnet([FromBody] TorrentControllerUploadMagnetRequest request)
        {
            await _torrents.UploadMagnet(request.MagnetLink, null, request.AutoDownload, request.AutoUnpack, request.AutoDelete);

            return Ok();
        }

        [HttpPost]
        [Route("Delete/{id}")]
        public async Task<ActionResult> Delete(Guid id, [FromBody] TorrentControllerDeleteRequest request)
        {
            await _torrents.Delete(id, request.DeleteData, request.DeleteRdTorrent, request.DeleteLocalFiles);

            return Ok();
        }

        [HttpGet]
        [Route("Download/{id}")]
        public async Task<ActionResult> Download(Guid id)
        {
            await _torrents.Unrestrict(id);

            return Ok();
        }
        
        [HttpGet]
        [Route("Unpack/{id}")]
        public async Task<ActionResult> Unpack(Guid id)
        {
            var downloads = await _downloads.GetForTorrent(id);

            foreach (var download in downloads)
            {
                await _torrents.Unpack(download.DownloadId);
            }

            return Ok();
        }
    }

    public class TorrentControllerUploadFileRequest
    {
        public Boolean AutoDownload { get; set; }
        public Boolean AutoUnpack { get; set; }
        public Boolean AutoDelete { get; set; }
    }

    public class TorrentControllerUploadMagnetRequest
    {
        public String MagnetLink { get; set; }
        public Boolean AutoDownload { get; set; }
        public Boolean AutoUnpack { get; set; }
        public Boolean AutoDelete { get; set; }
    }

    public class TorrentControllerDeleteRequest
    {
        public Boolean DeleteData { get; set; }
        public Boolean DeleteRdTorrent { get; set; }
        public Boolean DeleteLocalFiles { get; set; }
    }
}