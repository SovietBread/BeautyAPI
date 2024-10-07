using Microsoft.AspNetCore.Mvc;
using Data;
using Models;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Drawing;
using System.Security.Cryptography;

namespace Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SalonsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SalonsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> LoginSalon()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            if (user == null)
            {
                return Unauthorized("Invalid access token.");
            }

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var data = JsonDocument.Parse(body).RootElement;

            if (!data.TryGetProperty("login", out var salonLoginProp) || 
                !data.TryGetProperty("password", out var salonPasswordProp))
            {
                return BadRequest("Salon login and password are required.");
            }

            var salonLogin = salonLoginProp.GetString();
            var salonPassword = salonPasswordProp.GetString();

            var salon = await _context.Salons
                .FirstOrDefaultAsync(s => s.Name == salonLogin);

            if (salon == null || !BCrypt.Net.BCrypt.Verify(salonPassword, salon.PasswordHash))
            {
                return Unauthorized("Invalid salon login or password.");
            }

            if (_context.Employees.Any(e => e.UserId == user.Id && e.SalonId == salon.Id))
            {
                return Conflict("User is already associated with this salon.");
            }

            var employee = new Employee
            {
                UserId = user.Id,
                SalonId = salon.Id,
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Employees.Add(employee);

            if (user.UserType == "creator")
            {
                var unlockCode = new UnlockCode
                {
                    UserId = user.Id,
                    SalonId = salon.Id,
                    Code = GenerateUnlockCode(),
                    BlockType = "salon",
                    QrCode = GenerateQrCodeImage(user.Id.ToString())
                };

                _context.UnlockCodes.Add(unlockCode);
            }

            await _context.SaveChangesAsync();

            return Ok("Logged in successfully.");
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateSalon()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();

            var user = _context.Users.FirstOrDefault(u => u.AccessToken == accessToken);
            if (user == null)
            {
                return Unauthorized("Invalid access token.");
            }

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

            if (data == null || !data.TryGetValue("name", out var salonName) || 
                !data.TryGetValue("password", out var password))
            {
                return BadRequest("Salon name and password are required.");
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var salon = new Salon
            {
                Name = salonName,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow
            };

            _context.Salons.Add(salon);
            await _context.SaveChangesAsync();

            var employee = new Employee
            {
                UserId = user.Id,
                SalonId = salon.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            return Ok("Salon created and employee added successfully.");
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSalon(int id)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();

            var user = _context.Users.FirstOrDefault(u => u.AccessToken == accessToken);
            if (user == null)
            {
                return Forbid();
            }

            var employee = await _context.Employees
                .Where(e => e.UserId == user.Id && e.SalonId == id)
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                return Forbid();
            }

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var data = JsonDocument.Parse(body).RootElement;

            if (data.TryGetProperty("name", out var nameProp))
            {
                var salon = await _context.Salons.FindAsync(id);
                if (salon == null)
                {
                    return NotFound();
                }

                salon.Name = nameProp.GetString();
            }

            if (data.TryGetProperty("password", out var passwordProp))
            {
                var salon = await _context.Salons.FindAsync(id);
                if (salon == null)
                {
                    return NotFound();
                }

                salon.PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordProp.GetString());
            }

            await _context.SaveChangesAsync();
            return Ok("Salon updated successfully.");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSalon(int id)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            if (user == null)
            {
                return Unauthorized("Invalid access token or insufficient permissions.");
            }

            Console.WriteLine(user.Id);
            Console.WriteLine(id);

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == user.Id && e.SalonId == id);

            if (employee == null)
            {
                return Unauthorized("User is not an employee of the salon.");
            }

            var salon = await _context.Salons.FindAsync(id);
            if (salon == null)
            {
                return NotFound("Salon not found.");
            }

            var appointments = await _context.Appointments
                .Where(a => a.SalonId == id)
                .ToListAsync();

            if (appointments.Any())
            {
                _context.Appointments.RemoveRange(appointments);
            }

            _context.Salons.Remove(salon);
            await _context.SaveChangesAsync();

            return Ok("Salon and related appointments deleted successfully.");
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSalonById(int id)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            if (user == null)
            {
                return Unauthorized("Invalid access token.");
            }

            var salon = await _context.Salons
                .Where(s => s.Id == id)
                .Select(s => new
                {
                    s.Name,
                    Employees = _context.Employees
                        .Where(e => e.SalonId == s.Id)
                        .Select(e => new
                        {
                            e.Id,
                            e.UserId,
                            User = _context.Users
                                .Where(u => u.Id == e.UserId)
                                .Select(u => new
                                {
                                    u.Login,
                                    u.UserType
                                })
                                .FirstOrDefault()
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (salon == null)
            {
                return NotFound("Salon not found.");
            }

            return Ok(salon);
        }

        [HttpGet("{salonId}/qrcode")]
        public async Task<IActionResult> GetSalonQrCode(int salonId)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            if (user == null)
            {
                return Unauthorized("Invalid access token.");
            }

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == user.Id && e.SalonId == salonId);

            if (employee == null)
            {
                return Unauthorized("User is not an employee of the salon.");
            }

            var unlockCode = await _context.UnlockCodes
                .FirstOrDefaultAsync(uc => uc.SalonId == salonId);

            if (unlockCode == null)
            {
                return NotFound("Unlock code not found for the salon.");
            }

            return File(unlockCode.QrCode, "image/png");
        }

        [HttpDelete("{salonId}/activation")]
        public async Task<IActionResult> ResetSalonActivation(int salonId)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            if (user == null || user.UserType != "creator")
            {
                return Unauthorized("Invalid access token or insufficient permissions.");
            }

            var unlockCode = await _context.UnlockCodes
                .FirstOrDefaultAsync(uc => uc.SalonId == salonId && uc.UserId == user.Id && uc.BlockType == "salon");

            if (unlockCode == null)
            {
                return NotFound("Unlock code for activation not found.");
            }

            _context.UnlockCodes.Remove(unlockCode);
            await _context.SaveChangesAsync();

            return Ok("Salon activation reset.");
        }

        [HttpGet("{salonId}/check-activation")]
        public async Task<IActionResult> CheckSalonActivation(int salonId)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            if (user == null)
            {
                return Unauthorized("Invalid access token.");
            }

            var unlockCode = await _context.UnlockCodes
                .FirstOrDefaultAsync(uc => uc.SalonId == salonId && uc.UserId == user.Id && uc.BlockType == "salon");

            if (unlockCode == null)
            {
                return NotFound("Unlock code for activation not found.");
            }

            int maxWaitTimeInSeconds = 60;
            int pollIntervalInSeconds = 5;
            int elapsedTime = 0;

            while (elapsedTime < maxWaitTimeInSeconds)
            {
                unlockCode = await _context.UnlockCodes
                    .FirstOrDefaultAsync(uc => uc.UserId == user.Id && uc.BlockType == "salon");

                if (unlockCode == null)
                {
                    return Ok("Salon activated.");
                }

                await Task.Delay(pollIntervalInSeconds * 1000);
                elapsedTime += pollIntervalInSeconds;
            }

            return StatusCode(202, "Salon is not activated yet.");
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMySalons()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            if (user == null)
            {
                return Unauthorized("Invalid access token.");
            }

            var salons = await _context.Employees
                .Where(e => e.UserId == user.Id)
                .Select(e => new
                {
                    SalonId = e.SalonId,
                    Salon = _context.Salons
                        .Where(s => s.Id == e.SalonId)
                        .Select(s => new
                        {
                            s.Id,
                            s.Name
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(salons.Select(s => s.Salon));
        }

        [HttpPost("masters/create")]
        public async Task<IActionResult> CreateMaster()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            if (user == null)
            {
                return Unauthorized("Invalid access token.");
            }

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var data = JsonDocument.Parse(body).RootElement;

            if (!data.TryGetProperty("salonId", out var salonIdProp) || 
                !data.TryGetProperty("name", out var nameProp))
            {
                return BadRequest("Salon ID and name are required.");
            }

            var salonId = salonIdProp.GetInt32();
            var name = nameProp.GetString();

            
            if (!data.TryGetProperty("password", out var passwordProp))
            {
                return BadRequest("Password is required.");
            }

            var password = passwordProp.GetString();

            if (string.IsNullOrEmpty(password))
            {
                return BadRequest("Password must be at least 4 characters long.");
            }

            var salon = await _context.Salons.FindAsync(salonId);
            if (salon == null)
            {
                return NotFound("Salon not found.");
            }

            var master = new Master
            {
                SalonId = salonId,
                Name = name,
                Password = password,
                CreatedAt = DateTime.UtcNow
            };

            _context.Masters.Add(master);
            await _context.SaveChangesAsync();

            var salary = new Salary
            {
                MasterId = master.Id,
                Balance = 0
            };
            _context.Salary.Add(salary);

            var salonProcedures = await _context.Procedures
                                                .Where(p => p.SalonId == salonId)
                                                .ToListAsync();

            if (data.TryGetProperty("procedures", out var proceduresProp) && proceduresProp.ValueKind == JsonValueKind.Array)
            {
                var procedures = proceduresProp.EnumerateArray().ToArray();

                for (int i = 0; i < procedures.Length; i++)
                {
                    var percentage = procedures[i].GetInt32();
                    var procedure = salonProcedures.ElementAtOrDefault(i);

                    if (procedure != null)
                    {
                        var odsetek = new Odsetek
                        {
                            MasterId = master.Id,
                            ProcedureId = procedure.Id,
                            Percentage = percentage
                        };

                        _context.Odsetek.Add(odsetek);
                    }
                }
            }
            else
            {
                foreach (var procedure in salonProcedures)
                {
                    var odsetek = new Odsetek
                    {
                        MasterId = master.Id,
                        ProcedureId = procedure.Id,
                        Percentage = 0
                    };

                    _context.Odsetek.Add(odsetek);
                }
            }

            await _context.SaveChangesAsync();

            return Ok("Master created successfully.");
        }

        [HttpPut("masters/{id}")]
        public async Task<IActionResult> UpdateMaster(int id)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            if (user == null)
            {
                return Unauthorized("Invalid access token.");
            }

            var master = await _context.Masters.FindAsync(id);
            if (master == null)
            {
                return NotFound("Master not found.");
            }

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var data = JsonDocument.Parse(body).RootElement;

            if (data.TryGetProperty("name", out var nameProp))
            {
                master.Name = nameProp.GetString();
            }

            if (data.TryGetProperty("procedures", out var proceduresProp) && proceduresProp.ValueKind == JsonValueKind.Array)
            {
                var procedures = proceduresProp.EnumerateArray().ToArray();
                var allProcedures = await _context.Procedures.ToListAsync();

                for (int i = 0; i < procedures.Length; i++)
                {
                    var percentage = procedures[i].GetInt32();
                    var procedure = allProcedures.ElementAtOrDefault(i);

                    if (procedure != null)
                    {
                        var odsetek = await _context.Odsetek
                            .FirstOrDefaultAsync(o => o.MasterId == master.Id && o.ProcedureId == procedure.Id);

                        if (odsetek != null)
                        {
                            odsetek.Percentage = percentage;
                        }
                        else
                        {
                            var newOdsetek = new Odsetek
                            {
                                MasterId = master.Id,
                                ProcedureId = procedure.Id,
                                Percentage = percentage
                            };
                            _context.Odsetek.Add(newOdsetek);
                        }
                    }
                }
            }

            if (data.TryGetProperty("password", out var passwordProp))
            {
                var password = passwordProp.GetString();
                if (string.IsNullOrEmpty(password) || password.Length < 4)
                {
                    return BadRequest("Password must be at least 4 characters long.");
                }

                master.Password = password;
            }

            await _context.SaveChangesAsync();
            return Ok("Master updated successfully.");
        }

        [HttpGet("masters/{masterId}/balance")]
        public async Task<IActionResult> GetMasterBalance(int masterId, [FromQuery] DateTime? date = null)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            if (user == null)
            {
                return Unauthorized("Invalid access token.");
            }

            var master = await _context.Masters
                .Include(m => m.Salary)
                .FirstOrDefaultAsync(m => m.Id == masterId);

            if (master == null)
            {
                return NotFound("Master not found.");
            }

            date ??= DateTime.Now.Date;

            var todaysOperations = await _context.OperationHistory
                .Where(op => op.MasterId == masterId && op.OperationDate.Date == date.Value.Date)
                .ToListAsync();

            var allExpenses = await _context.OperationHistory
                .Where(op => op.MasterId == masterId && op.OperationType == "expense")
                .ToListAsync();

            var allIncomes = await _context.OperationHistory
                .Where(op => op.MasterId == masterId && op.OperationType == "income")
                .ToListAsync();

            var response = new
            {
                masterId = master.Id,
                balance = master.Salary?.Balance ?? 0,
                todaysOperations,
                allExpenses,
                allIncomes
            };

            return Ok(response);
        }

        [HttpPost("masters/{masterId}/withdraw")]
        public async Task<IActionResult> WithdrawFromMasterBalance(int masterId)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            if (user == null)
            {
                return Unauthorized("Invalid access token.");
            }

            var master = await _context.Masters
                .Include(m => m.Salary) // Включаем Salary для доступа к балансу
                .FirstOrDefaultAsync(m => m.Id == masterId);

            if (master == null)
            {
                return NotFound("Master not found.");
            }

            if (master.Salary == null)
            {
                return BadRequest("Master has no salary record.");
            }

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            using var jsonDoc = JsonDocument.Parse(body);
            var jsonElement = jsonDoc.RootElement;

            if (!jsonElement.TryGetProperty("amount", out var amountProp) || !amountProp.TryGetDecimal(out var amount))
            {
                return BadRequest("Invalid or missing amount.");
            }

            if (amount <= 0)
            {
                return BadRequest("Withdrawal amount must be greater than zero.");
            }

            if (master.Salary.Balance < amount)
            {
                return BadRequest("Insufficient funds.");
            }

            // Списываем средства
            master.Salary.Balance -= amount;
            await _context.SaveChangesAsync();

            return Ok(new { masterId = master.Id, newBalance = master.Salary.Balance });
        }

        [HttpDelete("masters/{id}")]
        public async Task<IActionResult> DeleteMaster(int id)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            if (user == null)
            {
                return Unauthorized("Invalid access token.");
            }

            var master = await _context.Masters.FindAsync(id);
            if (master == null)
            {
                return NotFound("Master not found.");
            }

            master.IsTerminated = true;

            _context.Entry(master).Property(m => m.IsTerminated).IsModified = true;
            await _context.SaveChangesAsync();

            return Ok("Master has been marked as terminated successfully.");
        }

        [HttpPost("procedures/create")]
        public async Task<IActionResult> CreateProcedure()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            if (user == null)
            {
                return Unauthorized("Invalid access token.");
            }

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var data = JsonDocument.Parse(body).RootElement;

            if (!data.TryGetProperty("salonId", out var salonIdProp) || 
                !data.TryGetProperty("name", out var nameProp))
            {
                return BadRequest("Salon ID and name are required.");
            }

            var salonId = salonIdProp.GetInt32();
            var name = nameProp.GetString();

            var salon = await _context.Salons.FindAsync(salonId);
            if (salon == null)
            {
                return NotFound("Salon not found.");
            }

            var procedure = new Procedure
            {
                SalonId = salonId,
                Name = name,
                CreatedAt = DateTime.UtcNow
            };

            _context.Procedures.Add(procedure);
            await _context.SaveChangesAsync();

            return Ok("Procedure created successfully.");
        }

        [HttpPut("procedures/{id}")]
        public async Task<IActionResult> UpdateProcedure(int id)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            if (user == null)
            {
                return Unauthorized("Invalid access token.");
            }

            var procedure = await _context.Procedures.FindAsync(id);
            if (procedure == null)
            {
                return NotFound("Procedure not found.");
            }

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var data = JsonDocument.Parse(body).RootElement;

            if (data.TryGetProperty("name", out var nameProp))
            {
                procedure.Name = nameProp.GetString();
            }

            await _context.SaveChangesAsync();
            return Ok("Procedure updated successfully.");
        }

        [HttpDelete("procedures/{id}")]
        public async Task<IActionResult> DeleteProcedure(int id)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            if (user == null)
            {
                return Unauthorized("Invalid access token.");
            }

            var procedure = await _context.Procedures.FindAsync(id);
            if (procedure == null)
            {
                return NotFound("Procedure not found.");
            }

            // Обновляем связанные записи
            var appointments = _context.Appointments.Where(a => a.ProcedureId == id);
            foreach (var appointment in appointments)
            {
                appointment.ProcedureId = null; // Устанавливаем в null, если это допустимо
            }

            // Удаляем связанные записи
            _context.Appointments.RemoveRange(appointments);

            // Удаляем запись процедуры
            _context.Procedures.Remove(procedure);

            await _context.SaveChangesAsync();

            return Ok("Procedure and related appointments deleted successfully.");
        }

        [HttpGet("{salonId}/masters")]
        public async Task<IActionResult> GetMastersBySalon(int salonId)
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized("Invalid or missing token.");
                }

                var accessToken = authHeader.Substring("Bearer ".Length).Trim();
                var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);
                if (user == null)
                {
                    return Unauthorized("Invalid access token.");
                }

                var employee = await _context.Employees
                    .Where(e => e.UserId == user.Id && e.SalonId == salonId)
                    .FirstOrDefaultAsync();

                if (employee == null)
                {
                    return Unauthorized("User is not an employee of the salon.");
                }

                var masters = await _context.Masters
                    .Where(m => m.SalonId == salonId && !m.IsTerminated)
                    .Select(m => new 
                    {
                        m.Id,
                        m.Name,
                        m.SalonId,
                        m.CreatedAt,
                        m.Password,
                        Procedures = _context.Odsetek
                            .Where(o => o.MasterId == m.Id)
                            .Join(_context.Procedures,
                                o => o.ProcedureId,
                                p => p.Id,
                                (o, p) => new
                                {
                                    ProcedureName = p.Name,
                                    Percentage = o.Percentage
                                }).ToList()
                    })
                    .ToListAsync();

                return Ok(masters);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, "An unexpected error occurred.");
            }
        }

        [HttpGet("{salonId}/procedures")]
        public async Task<IActionResult> GetProceduresBySalon(int salonId)
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized("Invalid or missing token.");
                }

                var accessToken = authHeader.Substring("Bearer ".Length).Trim();
                var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);
                if (user == null)
                {
                    return Unauthorized("Invalid access token.");
                }

                var employee = await _context.Employees
                    .Where(e => e.UserId == user.Id && e.SalonId == salonId)
                    .FirstOrDefaultAsync();

                if (employee == null)
                {
                    return Unauthorized("User is not an employee of the salon.");
                }

                var procedures = await _context.Procedures
                    .Where(p => p.SalonId == salonId)
                    .ToListAsync();

                return Ok(procedures);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, "An unexpected error occurred.");
            }
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

        [HttpPost("appointment")]
        public async Task<IActionResult> CreateAppointment()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var data = JsonDocument.Parse(body).RootElement;

            if (!data.TryGetProperty("clientName", out var clientNameProp) ||
                !data.TryGetProperty("masterId", out var masterIdProp) ||
                !data.TryGetProperty("procedureId", out var procedureIdProp) ||
                !data.TryGetProperty("cashAmount", out var cashAmountProp) ||
                !data.TryGetProperty("cardAmount", out var cardAmountProp) ||
                !data.TryGetProperty("fakeAmount", out var fakeAmountProp) ||
                !data.TryGetProperty("grouponAmount", out var grouponAmountProp) ||
                !data.TryGetProperty("salonId", out var salonIdProp) ||
                !data.TryGetProperty("appointmentDate", out var appointmentDateProp))
            {
                return BadRequest("All fields are required.");
            }

            var salonId = salonIdProp.GetInt32();
            var masterId = masterIdProp.GetInt32();
            var procedureId = procedureIdProp.GetInt32();
            var cashAmount = cashAmountProp.GetDecimal();
            var cardAmount = cardAmountProp.GetDecimal();
            var fakeAmount = fakeAmountProp.GetDecimal();
            var grouponAmount = grouponAmountProp.GetDecimal();
            var appointmentDate = appointmentDateProp.GetDateTime();
            var clientName = clientNameProp.GetString();

            appointmentDate = DateTime.SpecifyKind(appointmentDate, DateTimeKind.Utc);

            var salon = await _context.Salons.FindAsync(salonId);
            if (salon == null)
            {
                return NotFound("Salon not found.");
            }

            var master = await _context.Masters.FindAsync(masterId);
            if (master == null)
            {
                return NotFound("Master not found.");
            }

            var procedure = await _context.Procedures.FindAsync(procedureId);
            if (procedure == null)
            {
                return NotFound("Procedure not found.");
            }

            string? comment = null;
            if (data.TryGetProperty("comment", out var commentProp))
            {
                comment = commentProp.GetString();
            }

            var appointment = new Appointment
            {
                ClientName = clientName,
                MasterId = masterId,
                ProcedureId = procedureId,
                CashAmount = cashAmount,
                CardAmount = cardAmount,
                FakeAmount = fakeAmount,
                GrouponAmount = grouponAmount,
                SalonId = salonId,
                AppointmentDate = appointmentDate,
                Comment = comment
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var odsetek = await _context.Odsetek
                .FirstOrDefaultAsync(o => o.MasterId == masterId && o.ProcedureId == procedureId);

            if (odsetek == null)
            {
                return BadRequest("No percentage found for this master and procedure.");
            }

            var totalAmount = cashAmount + cardAmount + fakeAmount + grouponAmount;
            var percentage = odsetek.Percentage;
            var amountToAdd = (totalAmount * percentage) / 100;

            var salary = await _context.Salary.FirstOrDefaultAsync(s => s.MasterId == masterId);
            if (salary == null)
            {
                salary = new Salary
                {
                    MasterId = masterId,
                    Balance = amountToAdd
                };
                _context.Salary.Add(salary);
            }
            else
            {
                salary.Balance += amountToAdd;
                _context.Salary.Update(salary);
            }

            var operationHistory = new OperationHistory
            {
                MasterId = masterId,
                Amount = amountToAdd,
                OperationType = "income",
                OperationDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };

            _context.OperationHistory.Add(operationHistory);

            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAppointmentById), new { id = appointment.Id }, appointment);
        }


        [HttpPut("appointment/{id}")]
        public async Task<IActionResult> UpdateAppointment(int id)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var data = JsonDocument.Parse(body).RootElement;

            if (!data.TryGetProperty("clientName", out var clientNameProp) ||
                !data.TryGetProperty("masterId", out var masterIdProp) ||
                !data.TryGetProperty("procedureId", out var procedureIdProp) ||
                !data.TryGetProperty("cashAmount", out var cashAmountProp) ||
                !data.TryGetProperty("cardAmount", out var cardAmountProp) ||
                !data.TryGetProperty("fakeAmount", out var fakeAmountProp) ||
                !data.TryGetProperty("grouponAmount", out var grouponAmountProp) ||
                !data.TryGetProperty("salonId", out var salonIdProp) ||
                !data.TryGetProperty("appointmentDate", out var appointmentDateProp))
            {
                return BadRequest("All fields are required.");
            }

            var salonId = salonIdProp.GetInt32();
            var masterId = masterIdProp.GetInt32();
            var procedureId = procedureIdProp.GetInt32();
            var cashAmount = cashAmountProp.GetDecimal();
            var cardAmount = cardAmountProp.GetDecimal();
            var fakeAmount = fakeAmountProp.GetDecimal();
            var grouponAmount = grouponAmountProp.GetDecimal();
            var appointmentDate = appointmentDateProp.GetDateTime();
            var clientName = clientNameProp.GetString();

            // Обрабатываем комментарий
            string comment = data.TryGetProperty("comment", out var commentProp) ? commentProp.GetString() : null;

            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                return NotFound("Appointment not found.");
            }

            appointment.ClientName = clientName;
            appointment.MasterId = masterId;
            appointment.ProcedureId = procedureId;
            appointment.CashAmount = cashAmount;
            appointment.CardAmount = cardAmount;
            appointment.FakeAmount = fakeAmount;
            appointment.GrouponAmount = grouponAmount;
            appointment.SalonId = salonId;
            appointment.AppointmentDate = appointmentDate;
            appointment.Comment = comment; // Сохраняем комментарий

            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

            return Ok(appointment);
        }

        [HttpDelete("appointment/{id}")]
        public async Task<IActionResult> DeleteAppointment(int id)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                return NotFound("Appointment not found.");
            }

            _context.Appointments.Remove(appointment);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAppointmentById(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Master)
                .Include(a => a.Procedure)
                .Include(a => a.Salon)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                return NotFound("Appointment not found.");
            }

            return Ok(appointment);
        }

        [HttpGet("{salonId}/appointments")]
        public async Task<IActionResult> GetAppointments(int salonId, [FromQuery] DateTime? date = null)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            var selectedDate = (date ?? DateTime.UtcNow).Date;
            selectedDate = DateTime.SpecifyKind(selectedDate, DateTimeKind.Utc);

            var appointments = await _context.Appointments
                .Where(a => a.SalonId == salonId && a.AppointmentDate.Date == selectedDate)
                .Select(a => new 
                {
                    a.Id,
                    a.ClientName,
                    MasterId = a.Master != null ? a.Master.Id : (int?)null,
                    MasterName = a.Master != null ? a.Master.Name : null,
                    ProcedureId = a.Procedure != null ? a.Procedure.Id : (int?)null,
                    ProcedureName = a.Procedure != null ? a.Procedure.Name : null,
                    a.CashAmount,
                    a.CardAmount,
                    a.FakeAmount,
                    a.GrouponAmount,
                    a.Comment
                })
                .ToListAsync();


            if (appointments == null || !appointments.Any())
            {
                return NotFound("No appointments found for the specified salon and date.");
            }

            return Ok(appointments);
        }

        [HttpPost("income")]
        public async Task<IActionResult> CreateIncome([FromBody] JsonDocument data)
        {
            if (!data.RootElement.TryGetProperty("salonId", out var salonIdProp) ||
                !data.RootElement.TryGetProperty("cashAmount", out var cashAmountProp) ||
                !data.RootElement.TryGetProperty("cardAmount", out var cardAmountProp) ||
                !data.RootElement.TryGetProperty("incomeDate", out var incomeDateProp))
            {
                return BadRequest("All fields are required.");
            }

            var salonId = salonIdProp.GetInt32();
            var cashAmount = cashAmountProp.GetDecimal();
            var cardAmount = cardAmountProp.GetDecimal();
            var incomeDate = incomeDateProp.GetDateTime();
            string comment = data.RootElement.TryGetProperty("comment", out var commentProp) ? commentProp.GetString() : null;
            
            var salon = await _context.Salons.FindAsync(salonId);
            if (salon == null)
            {
                return NotFound("Salon not found.");
            }

            var income = new Income
            {
                SalonId = salonId,
                CashAmount = cashAmount,
                CardAmount = cardAmount,
                IncomeDate = incomeDate,
                Comment = comment
            };

            _context.Income.Add(income);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetIncomeById), new { id = income.Id }, income);
        }


        [HttpGet("{salonId}/income")]
        public async Task<IActionResult> GetIncome([FromRoute] int salonId, [FromQuery] DateTime? date = null)
        {
            // Используем текущую дату, если параметр date не указан
            date ??= DateTime.UtcNow.Date;

            // Приведение даты к UTC, если дата имеет Kind.Unspecified
            if (date.Value.Kind == DateTimeKind.Unspecified)
            {
                date = DateTime.SpecifyKind(date.Value, DateTimeKind.Utc);
            }

            var incomeRecords = await _context.Income
                .Where(i => i.SalonId == salonId && i.IncomeDate.Date == date.Value.Date)
                .Select(i => new
                {
                    i.Id,
                    i.CashAmount,
                    i.CardAmount,
                    i.IncomeDate,
                    i.Comment // Включаем комментарий в результат
                })
                .ToListAsync();

            // Проверяем, найдены ли записи
            if (incomeRecords == null || !incomeRecords.Any())
            {
                return NotFound("No income records found for the specified salon and date.");
            }

            return Ok(incomeRecords);
        }

        [HttpGet("income/{id}")]
        public async Task<IActionResult> GetIncomeById(int id)
        {
            var income = await _context.Income.FindAsync(id);

            if (income == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                income.Id,
                income.CashAmount,
                income.CardAmount,
                IncomeDate = income.IncomeDate,
                income.Comment
            });
        }

        [HttpPut("income/{id}")]
        public async Task<IActionResult> UpdateIncome(int id, [FromBody] JsonDocument data)
        {
            if (!data.RootElement.TryGetProperty("cashAmount", out var cashAmountProp) ||
                !data.RootElement.TryGetProperty("cardAmount", out var cardAmountProp) ||
                !data.RootElement.TryGetProperty("incomeDate", out var incomeDateProp) ||
                !data.RootElement.TryGetProperty("comment", out var commentProp))
            {
                return BadRequest("All fields are required.");
            }

            var income = await _context.Income.FindAsync(id);
            if (income == null)
            {
                return NotFound("Income not found.");
            }

            income.CashAmount = cashAmountProp.GetDecimal();
            income.CardAmount = cardAmountProp.GetDecimal();
            income.IncomeDate = incomeDateProp.GetDateTime();
            income.Comment = commentProp.GetString();

            _context.Income.Update(income);
            await _context.SaveChangesAsync();

            return Ok(income);
        }

        [HttpDelete("income/{id}")]
        public async Task<IActionResult> DeleteIncome(int id)
        {
            var income = await _context.Income.FindAsync(id);
            if (income == null)
            {
                return NotFound("Income not found.");
            }

            _context.Income.Remove(income);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("expense")]
        public async Task<IActionResult> CreateExpense([FromBody] JsonDocument data)
        {
            if (!data.RootElement.TryGetProperty("salonId", out var salonIdProp) ||
                !data.RootElement.TryGetProperty("cashAmount", out var cashAmountProp) ||
                !data.RootElement.TryGetProperty("cardAmount", out var cardAmountProp) ||
                !data.RootElement.TryGetProperty("expenseDate", out var expenseDateProp) ||
                !data.RootElement.TryGetProperty("comment", out var commentProp) ||
                !data.RootElement.TryGetProperty("isSalary", out var isSalaryProp) ||
                !data.RootElement.TryGetProperty("employeeId", out var employeeIdProp) ||
                !data.RootElement.TryGetProperty("deductFromCash", out var deductFromCashProp))
            {
                return BadRequest("All fields are required.");
            }

            int salonId;
            if (salonIdProp.ValueKind == JsonValueKind.String)
            {
                Console.WriteLine($"Received salonId as string: {salonIdProp.GetString()}");
                if (!int.TryParse(salonIdProp.GetString(), out salonId))
                {
                    return BadRequest("Invalid salonId.");
                }
            }
            else
            {
                salonId = salonIdProp.GetInt32();
            }

            decimal cashAmount;
            if (cashAmountProp.ValueKind == JsonValueKind.String)
            {
                Console.WriteLine($"Received cashAmount as string: {cashAmountProp.GetString()}");
                if (!decimal.TryParse(cashAmountProp.GetString(), out cashAmount))
                {
                    return BadRequest("Invalid cashAmount.");
                }
            }
            else
            {
                cashAmount = cashAmountProp.GetDecimal();
            }

            decimal cardAmount;
            if (cardAmountProp.ValueKind == JsonValueKind.String)
            {
                Console.WriteLine($"Received cardAmount as string: {cardAmountProp.GetString()}");
                if (!decimal.TryParse(cardAmountProp.GetString(), out cardAmount))
                {
                    return BadRequest("Invalid cardAmount.");
                }
            }
            else
            {
                cardAmount = cardAmountProp.GetDecimal();
            }

            var expenseDate = expenseDateProp.GetDateTime();
            var comment = commentProp.GetString();
            var isSalary = isSalaryProp.GetBoolean();
            
            int employeeId;
            if (employeeIdProp.ValueKind == JsonValueKind.String)
            {
                Console.WriteLine($"Received employeeId as string: {employeeIdProp.GetString()}");
                if (!int.TryParse(employeeIdProp.GetString(), out employeeId))
                {
                    return BadRequest("Invalid employeeId.");
                }
            }
            else
            {
                employeeId = employeeIdProp.GetInt32();
            }

            var deductFromCash = deductFromCashProp.GetBoolean();

            var salon = await _context.Salons.FindAsync(salonId);
            if (salon == null)
            {
                return NotFound("Salon not found.");
            }

            var expense = new Expense
            {
                SalonId = salonId,
                CashAmount = cashAmount,
                CardAmount = cardAmount,
                ExpenseDate = expenseDate,
                Comment = comment,
                IsSalary = isSalary,
                EmployeeId = employeeId,
                DeductFromCash = deductFromCash
            };

            _context.Expense.Add(expense);
            await _context.SaveChangesAsync();

            if (isSalary)
            {
                var operationHistory = new OperationHistory
                {
                    MasterId = employeeId,
                    Amount = cashAmount + cardAmount,
                    OperationType = "expense",
                    OperationDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
                };

                _context.OperationHistory.Add(operationHistory);
                await _context.SaveChangesAsync();
            }

            if (isSalary && deductFromCash)
            {
                return await WithdrawFromMasterBalance(employeeId, cashAmount);
            }

            return CreatedAtAction(nameof(GetExpenseById), new { id = expense.Id }, expense);
        }

        private async Task<IActionResult> WithdrawFromMasterBalance(int masterId, decimal amount)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);

            if (user == null)
            {
                return Unauthorized("Invalid access token.");
            }

            var master = await _context.Masters
                .Include(m => m.Salary)
                .FirstOrDefaultAsync(m => m.Id == masterId);

            if (master == null)
            {
                return NotFound("Master not found.");
            }

            if (master.Salary == null)
            {
                return BadRequest("Master has no salary record.");
            }

            if (master.Salary.Balance < amount)
            {
                return BadRequest("Insufficient funds.");
            }

            master.Salary.Balance -= amount;
            await _context.SaveChangesAsync();

            return Ok(new { masterId = master.Id, newBalance = master.Salary.Balance });
        }

        [HttpGet("{salonId}/expense")]
        public async Task<IActionResult> GetExpenses([FromRoute] int salonId, [FromQuery] DateTime? date = null)
        {
            // Используем текущую дату, если параметр date не указан
            date ??= DateTime.UtcNow.Date;

            // Приведение даты к UTC, если дата имеет Kind.Unspecified
            if (date.Value.Kind == DateTimeKind.Unspecified)
            {
                date = DateTime.SpecifyKind(date.Value, DateTimeKind.Utc);
            }

            var expenseRecords = await _context.Expense
                .Where(e => e.SalonId == salonId && e.ExpenseDate.Date == date.Value.Date)
                .Select(e => new
                {
                    e.Id,
                    e.CashAmount,
                    e.CardAmount,
                    e.ExpenseDate,
                    e.Comment
                })
                .ToListAsync();

            // Проверяем, найдены ли записи
            if (expenseRecords == null || !expenseRecords.Any())
            {
                return NotFound("No expense records found for the specified salon and date.");
            }

            return Ok(expenseRecords);
        }

        [HttpGet("expense/{id}")]
        public async Task<IActionResult> GetExpenseById(int id)
        {
            var expense = await _context.Expense.FindAsync(id);

            if (expense == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                expense.Id,
                expense.CashAmount,
                expense.CardAmount,
                ExpenseDate = expense.ExpenseDate,
                expense.Comment
            });
        }

        [HttpPut("expense/{id}")]
        public async Task<IActionResult> UpdateExpense(int id, [FromBody] JsonDocument data)
        {
            if (!data.RootElement.TryGetProperty("cashAmount", out var cashAmountProp) ||
                !data.RootElement.TryGetProperty("cardAmount", out var cardAmountProp) ||
                !data.RootElement.TryGetProperty("expenseDate", out var expenseDateProp) ||
                !data.RootElement.TryGetProperty("comment", out var commentProp))
            {
                return BadRequest("All fields are required.");
            }

            var expense = await _context.Expense.FindAsync(id);
            if (expense == null)
            {
                return NotFound("Expense not found.");
            }

            expense.CashAmount = cashAmountProp.GetDecimal();
            expense.CardAmount = cardAmountProp.GetDecimal();
            expense.ExpenseDate = expenseDateProp.GetDateTime();
            expense.Comment = commentProp.GetString();

            _context.Expense.Update(expense);
            await _context.SaveChangesAsync();

            return Ok(expense);
        }

        [HttpDelete("expense/{id}")]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            var expense = await _context.Expense.FindAsync(id);
            if (expense == null)
            {
                return NotFound("Expense not found.");
            }

            _context.Expense.Remove(expense);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("{salonId}/statistics")]
        public async Task<IActionResult> GetDailyStatistics(int salonId)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid or missing token.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AccessToken == accessToken);
            if (user == null)
            {
                return Unauthorized("User not found.");
            }

            var eventDates = await _context.Appointments
                .Where(a => a.SalonId == salonId)
                .Select(a => a.AppointmentDate.Date)
                .Union(_context.Income.Where(i => i.SalonId == salonId).Select(i => i.IncomeDate.Date))
                .Union(_context.Expense.Where(e => e.SalonId == salonId).Select(e => e.ExpenseDate.Date))
                .Distinct()
                .OrderBy(date => date)
                .ToListAsync();

            var statistics = new List<object>();

            foreach (var date in eventDates)
            {
                var totalCashAppointments = await _context.Appointments
                    .Where(a => a.SalonId == salonId && a.AppointmentDate.Date == date)
                    .SumAsync(a => a.CashAmount);

                var totalCashIncome = await _context.Income
                    .Where(i => i.SalonId == salonId && i.IncomeDate.Date == date)
                    .SumAsync(i => i.CashAmount);

                var totalCashExpense = await _context.Expense
                    .Where(e => e.SalonId == salonId && e.ExpenseDate.Date == date)
                    .SumAsync(e => e.CashAmount);

                var totalCardAppointments = await _context.Appointments
                    .Where(a => a.SalonId == salonId && a.AppointmentDate.Date == date)
                    .SumAsync(a => a.CardAmount);

                var totalCardIncome = await _context.Income
                    .Where(i => i.SalonId == salonId && i.IncomeDate.Date == date)
                    .SumAsync(i => i.CardAmount);

                var totalCardExpense = await _context.Expense
                    .Where(e => e.SalonId == salonId && e.ExpenseDate.Date == date)
                    .SumAsync(e => e.CardAmount);

                var totalFakeAppointments = await _context.Appointments
                    .Where(a => a.SalonId == salonId && a.AppointmentDate.Date == date)
                    .SumAsync(a => a.FakeAmount);

                var totalGrouponAppointments = await _context.Appointments
                    .Where(a => a.SalonId == salonId && a.AppointmentDate.Date == date)
                    .SumAsync(a => a.GrouponAmount);

                statistics.Add(new
                {
                    Date = date,
                    TotalCash = totalCashAppointments + totalCashIncome - totalCashExpense,
                    TotalCard = totalCardAppointments + totalCardIncome - totalCardExpense,
                    TotalFake = totalFakeAppointments,
                    TotalGroupon = totalGrouponAppointments
                });
            }

            if (!statistics.Any())
            {
                return NotFound("No statistics found.");
            }

            return Ok(statistics);
        }
    }
}
