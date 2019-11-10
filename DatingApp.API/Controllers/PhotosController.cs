using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DatingApp.API.Data;
using DatingApp.API.DTOs;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DatingApp.API.Controllers
{
    [Authorize]
    [Route("api/users/{userId}/photos")]
    [ApiController]
    public class PhotosController : ControllerBase
    {
        public readonly IDatingRepository _repository;
        public readonly IMapper _mapper;
        public readonly IOptions<CloudinarySettings> _cloudinaryConfig;
        private Cloudinary _cloudinary;

        public PhotosController(IDatingRepository repository, IMapper mapper, IOptions<CloudinarySettings> cloudinaryConfig)
        {
            _cloudinaryConfig = cloudinaryConfig;
            _mapper = mapper;
            _repository = repository;

            Account account = new Account(
                _cloudinaryConfig.Value.CloudName,
                _cloudinaryConfig.Value.ApiKey,
                _cloudinaryConfig.Value.ApiSecret
            );
            
            _cloudinary = new Cloudinary(account);                        
        }
        [HttpGet("{id}", Name = "GetPhoto")]
        public async Task<IActionResult> GetPhoto(int id)
        {
            var photoFromRepo = await _repository.GetPhoto(id);

            var photo = _mapper.Map<PhotoForReturnDto>(photoFromRepo);

            return Ok(photo);
        }
        [HttpPost]
        public async Task<IActionResult> AddPhotoForUser(int userId,
            [FromForm] PhotoForCreationDto photoForCreationDto)
        {
            if(userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();
            var userFromRepo = await _repository.GetUser(userId);
            var file = photoForCreationDto.File;
            var uploadedResult = new ImageUploadResult();
            
            if(file.Length > 0)
            {
                using (var stream = file.OpenReadStream())
                {
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(file.Name, stream),
                        Transformation = new Transformation().Width(500).Height(500).Crop("fill").Gravity("face")
                    };

                    uploadedResult = _cloudinary.Upload(uploadParams);
                }

                photoForCreationDto.Url = uploadedResult.Uri.ToString();
                photoForCreationDto.PublicId = uploadedResult.PublicId;

                var photo = _mapper.Map<Photo>(photoForCreationDto);

                if(!userFromRepo.Photos.Any(u => u.IsMain))
                    photo.IsMain = true;
                
                userFromRepo.Photos.Add(photo);

                if(await _repository.SaveAll())
                {
                    var photoToReturn = _mapper.Map<PhotoForReturnDto>(photo);
                    return CreatedAtRoute("GetPhoto", new { id = photo.Id}, photoToReturn);                    
                }                
            }


            return BadRequest("Could not add photo");
        }

        [HttpPost("{id}/setMain")]
        public async Task<IActionResult> SetMainPhoto(int userId, int id)
        {
            if(userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();
            
            var userFromRepo = await _repository.GetUser(userId);

            if(!userFromRepo.Photos.Any(p => p.Id == id))
                return Unauthorized();
            
            var photoFromRepo = await _repository.GetPhoto(id);

            if(photoFromRepo.IsMain)
                return BadRequest("This is main photo");

            var currentMainPhoto = await _repository.GetMainPhotoForUser(userId);
            currentMainPhoto.IsMain = false;

            photoFromRepo.IsMain = true;

            if(await _repository.SaveAll())
            {                
                return NoContent();   
            } 

            return BadRequest("Could not set photo to main");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePhoto(int userId, int id)
        {
            if(userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();
            
            var userFromRepo = await _repository.GetUser(userId);

            if(!userFromRepo.Photos.Any(p => p.Id == id))
                return Unauthorized();
            
            var photoFromRepo = await _repository.GetPhoto(id);
            
            if(photoFromRepo.IsMain)
                return BadRequest("YOu cannot delete your main photo");
            if(photoFromRepo.PublicId != null)
            {
                var result = _cloudinary.Destroy(new DeletionParams(photoFromRepo.PublicId));

                if(result.Result == "ok")
                {
                    _repository.Delete(photoFromRepo);
                }
            }
            
            if(photoFromRepo.PublicId == null)
            {
                _repository.Delete(photoFromRepo);
            }
            
            if(await _repository.SaveAll())
            {                
                return Ok();   
            } 

            return BadRequest("Could not delete photo");
        }
    }
}