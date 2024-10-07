using Microsoft.AspNetCore.Mvc;
using Data;
using Models;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Drawing;

namespace Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

            if (data == null || !data.TryGetValue("login", out var login) ||
                !data.TryGetValue("password", out var password) ||
                !data.TryGetValue("user_type", out var userType))
            {
                return BadRequest("Login, password, and user_type are required.");
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            var refreshToken = GenerateRefreshToken();
            var accessToken = GenerateAccessToken(refreshToken);

            var user = new User
            {
                Login = login,
                PasswordHash = passwordHash,
                UserType = userType,
                CreatedAt = DateTime.UtcNow,
                RefreshToken = refreshToken,
                AccessToken = accessToken
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            if (userType == "creator")
            {
                var unlockCode = GenerateUnlockCode();
                var unlockCodeEntity = new UnlockCode
                {
                    UserId = user.Id,
                    Code = unlockCode,
                    SalonId = null,
                    BlockType = "auth"
                };

                var qrCodeImage = GenerateQrCodeImage(unlockCode);
                unlockCodeEntity.QrCode = qrCodeImage;

                _context.UnlockCodes.Add(unlockCodeEntity);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserType = userType
            });
        }

        [HttpGet("qrcode")]
        public async Task<IActionResult> GetQrCode()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader == null || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Bearer token is missing or invalid.");
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == token);
            if (user == null)
            {
                return Unauthorized("Invalid token.");
            }

            var unlockCode = await _context.UnlockCodes
                .Where(uc => uc.UserId == user.Id)
                .Select(uc => new { uc.Code, uc.QrCode })
                .FirstOrDefaultAsync();

            if (unlockCode == null)
            {
                return NotFound("Unlock code not found.");
            }

            var qrCodeImageBase64 = Convert.ToBase64String(unlockCode.QrCode);
            var response = new
            {
                Code = unlockCode.Code,
                QrCodeImage = $"data:image/png;base64,{qrCodeImageBase64}"
            };

            return Ok(response);
        }

        [HttpDelete("qrcode/activate/{code}")]
        public async Task<IActionResult> ActivateQrCode(string code)
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader == null || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Bearer token is missing or invalid.");
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == token);
            if (user == null)
            {
                return Unauthorized("Invalid token.");
            }

            if (user.UserType != "creator")
            {
                return Unauthorized("User does not have permission to activate QR codes.");
            }

            var unlockCodeRecord = await _context.UnlockCodes
                .FirstOrDefaultAsync(uc => uc.Code == code);

            if (unlockCodeRecord == null)
            {
                return NotFound("Unlock code not found.");
            }

            _context.UnlockCodes.Remove(unlockCodeRecord);
            await _context.SaveChangesAsync();

            return Ok("Unlock code activated (deleted) successfully.");
        }

        [HttpGet("qrcode/activate/check-activation")]
        public async Task<IActionResult> CheckActivation()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            Console.WriteLine(authHeader);
            if (authHeader == null || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Bearer token is missing or invalid.");
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == token);
            if (user == null)
            {
                return Unauthorized("Invalid token.");
            }

            var unlockCode = await _context.UnlockCodes
                .FirstOrDefaultAsync(uc => uc.UserId == user.Id && uc.BlockType == "auth");
            
            if (unlockCode == null)
            {
                return Ok("Account already activated.");
            }

            int maxWaitTimeInSeconds = 60 * 10;
            int pollIntervalInSeconds = 5;
            int elapsedTime = 0;

            while (elapsedTime < maxWaitTimeInSeconds)
            {
                unlockCode = await _context.UnlockCodes
                    .FirstOrDefaultAsync(uc => uc.UserId == user.Id && uc.BlockType == "auth");

                if (unlockCode == null)
                {
                    return Ok("Account activated.");
                }

                await Task.Delay(pollIntervalInSeconds * 1000);
                elapsedTime += pollIntervalInSeconds;
            }

            return StatusCode(202, "Account is not activated yet.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

            if (data == null || !data.TryGetValue("login", out var login) ||
                !data.TryGetValue("password", out var password))
            {
                return BadRequest("Login and password are required.");
            }

            var user = _context.Users.FirstOrDefault(u => u.Login == login);
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                return Unauthorized("Invalid login or password.");
            }

            var newRefreshToken = GenerateRefreshToken();
            var newAccessToken = GenerateAccessToken(newRefreshToken);

            user.RefreshToken = newRefreshToken;
            user.AccessToken = newAccessToken;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                UserType = user.UserType
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshTokens()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var refreshToken = authHeader.Substring("Bearer ".Length).Trim();
            var user = _context.Users.FirstOrDefault(u => u.RefreshToken == refreshToken);
            if (user == null)
            {
                return Unauthorized("Invalid refresh token.");
            }

            var newRefreshToken = GenerateRefreshToken();
            var newAccessToken = GenerateAccessToken(newRefreshToken);

            user.RefreshToken = newRefreshToken;
            user.AccessToken = newAccessToken;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                access_token = newAccessToken,
                refresh_token = newRefreshToken
            });
        }

        [HttpPost("check-login")]
        public async Task<IActionResult> CheckLogin()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

            if (data == null || !data.TryGetValue("login", out var login))
            {
                return BadRequest("Login is required.");
            }

            var userExists = await _context.Users.AnyAsync(u => u.Login == login);
            if (userExists)
            {
                return BadRequest("Login already taken.");
            }

            return Ok("Login available.");
        }

        [HttpPost("log-error")]
        public async Task<IActionResult> LogError()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

            if (data == null || !data.TryGetValue("message", out var message))
            {
                return BadRequest("Error message is required.");
            }

            var exception = data.TryGetValue("exception", out var ex) ? ex : null;

            var errorLog = new ErrorLog
            {
                Message = message,
                Exception = exception,
                Timestamp = DateTime.UtcNow
            };

            _context.ErrorLogs.Add(errorLog);
            await _context.SaveChangesAsync();

            return Ok("Error logged successfully.");
        }

        [HttpGet("error-logs")]
        public async Task<IActionResult> GetErrorLogs()
        {
            var errorLogs = await _context.ErrorLogs
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync();

            return Ok(errorLogs);
        }

        private string GenerateRefreshToken()
        {
            using var rng = new RNGCryptoServiceProvider();
            var byteArray = new byte[32];
            rng.GetBytes(byteArray);
            return Convert.ToBase64String(byteArray);
        }

        private string GenerateAccessToken(string refreshToken)
        {
            if (refreshToken.Length < 32)
            {
                throw new InvalidOperationException("Refresh token is too short to be used as a signing key.");
            }

            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(refreshToken));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, "access"),
                }),
                Expires = DateTime.UtcNow.AddMinutes(15),
                SigningCredentials = creds
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private string GenerateUnlockCode()
        {
            using var rng = new RNGCryptoServiceProvider();
            var byteArray = new byte[16]; // 128 бит
            rng.GetBytes(byteArray);
            return Convert.ToBase64String(byteArray).Replace("=", "").Replace("+", "").Replace("/", "");
        }

        private byte[] GenerateQrCodeImage(string code)
        {
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(code, QRCodeGenerator.ECCLevel.Q);

            // Создаём QR-код в формате PNG с прозрачным фоном
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBytes = qrCode.GetGraphic(20, Color.Black, Color.Transparent);

            return qrCodeBytes;
        }

        private async Task<string> SaveQrCodeImageAsync(byte[] qrCodeImage)
        {
            var fileName = $"{Guid.NewGuid()}.png";
            var filePath = Path.Combine("wwwroot/qr-codes", fileName);

            await System.IO.File.WriteAllBytesAsync(filePath, qrCodeImage);

            return $"/qr-codes/{fileName}";
        }
    }
}
