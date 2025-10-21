using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;

namespace RestaurantManagementSystem
{
    // ==================== ENUMS ====================
    public enum UserRole
    {
        Admin,
        Manager,
        Staff
    }

    public enum OrderStatus
    {
        Pending,
        Processing,
        Completed,
        Cancelled
    }

    public enum LogLevel
    {
        INFO,
        WARNING,
        ERROR,
        DEBUG
    }



    // ==================== MODELS ====================
    public class User
    {
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public UserRole Role { get; set; }
        public string FullName { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }

        public User(string username, string passwordHash, UserRole role, string fullName)
        {
            Username = username;
            PasswordHash = passwordHash;
            Role = role;
            FullName = fullName;
            CreatedDate = DateTime.Now;
            IsActive = true;
        }
    }

    
    public class Dish
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string Category { get; set; }
        public bool IsAvailable { get; set; }
        public Dictionary<string, decimal> Ingredients { get; set; }
        public int SalesCount { get; set; }
        public decimal Cost { get; set; }
        public decimal ProfitMargin => Price > 0 ? Math.Round((Price - Cost) / Price * 100, 2) : 0;

        public Dish(string id, string name, string description, decimal price, string category)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? "";

            if (price < 0) throw new ArgumentException("Price cannot be negative");
            Price = Math.Round(price, 2);

            Category = category ?? "Món chính";
            IsAvailable = true;
            Ingredients = new Dictionary<string, decimal>();
            SalesCount = 0;
            Cost = 0;
        }

        public decimal CalculateCost(Dictionary<string, Ingredient> ingredients)
        {
            if (ingredients == null) throw new ArgumentNullException(nameof(ingredients));

            decimal cost = 0;
            foreach (var ing in Ingredients)
            {
                if (ingredients.ContainsKey(ing.Key))
                {
                    cost += ingredients[ing.Key].PricePerUnit * ing.Value;
                }
            }
            Cost = Math.Round(cost, 2);
            return Cost;
        }
    }
    public class Ingredient
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal MinQuantity { get; set; }
        public decimal PricePerUnit { get; set; }
        public DateTime LastUpdated { get; set; }

        public Ingredient(string id, string name, string unit, decimal quantity, decimal minQuantity, decimal pricePerUnit)
        {
            Id = id;
            Name = name;
            Unit = unit;
            Quantity = quantity;
            MinQuantity = minQuantity;
            PricePerUnit = pricePerUnit;
            LastUpdated = DateTime.Now;
        }

        public bool IsLowStock { get { return Quantity <= MinQuantity; } }
    }


     public class Combo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> DishIds { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal FinalPrice => Math.Round(OriginalPrice * (1 - DiscountPercent / 100), 2);
        public DateTime CreatedDate { get; set; }
        public int SalesCount { get; set; }
        public bool IsActive { get; set; }
        public decimal Cost { get; set; }
        public decimal ProfitMargin => FinalPrice > 0 ? Math.Round((FinalPrice - Cost) / FinalPrice * 100, 2) : 0;

        public Combo(string id, string name, string description, decimal discountPercent)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? "";
            DishIds = new List<string>();

            if (discountPercent < 0 || discountPercent > 100)
                throw new ArgumentException("DiscountPercent must be between 0 and 100");
            DiscountPercent = discountPercent;

            CreatedDate = DateTime.Now;
            SalesCount = 0;
            IsActive = true;
            Cost = 0;
        }

        public void CalculateOriginalPrice(Dictionary<string, Dish> dishes)
        {
            if (dishes == null) throw new ArgumentNullException(nameof(dishes));

            OriginalPrice = 0;
            Logger.Info($"Calculating combo '{Name}' price for {DishIds.Count} dishes", "Combo");

            if (DishIds.Count == 0)
            {
                Logger.Warning("Combo has no dishes!", "Combo");
                return;
            }

            foreach (var dishId in DishIds)
            {
                if (dishes.ContainsKey(dishId))
                {
                    var dish = dishes[dishId];
                    OriginalPrice += dish.Price;
                    Logger.Info($"Added dish '{dish.Name}' ({dishId}) - {dish.Price:N0}đ to combo. Total: {OriginalPrice:N0}đ", "Combo");
                }
                else
                {
                    Logger.Error($"Dish {dishId} not found in repository! Available dishes: {string.Join(", ", dishes.Keys.Take(5))}...", "Combo");
                    throw new KeyNotFoundException($"Dish {dishId} not found in repository");
                }
            }

            OriginalPrice = Math.Round(OriginalPrice, 2);
            Logger.Info($"Final combo '{Name}' price: {OriginalPrice:N0}đ", "Combo");
        }

        public decimal CalculateCost(Dictionary<string, Dish> dishes)
        {
            if (dishes == null) throw new ArgumentNullException(nameof(dishes));

            Cost = 0;
            foreach (var dishId in DishIds)
            {
                if (dishes.ContainsKey(dishId))
                {
                    Cost += dishes[dishId].Cost;
                }
            }
            // Round to avoid floating point errors
            Cost = Math.Round(Cost, 2);
            return Cost;
        }
    }




    public class OrderItem
    {
        public string ItemId { get; set; }
        public bool IsCombo { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get { return UnitPrice * Quantity; } }
        public string ItemName { get; set; }
    }

    public class Order
    {
        public string Id { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerAddress { get; set; }
        public List<OrderItem> Items { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public decimal TotalAmount { get { return Items.Sum(item => item.TotalPrice); } }
        public string StaffUsername { get; set; }
        public string Notes { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get { return TotalAmount - DiscountAmount; } }

        public Order(string id, string customerName, string staffUsername)
        {
            Id = id;
            CustomerName = customerName;
            StaffUsername = staffUsername;
            Items = new List<OrderItem>();
            Status = OrderStatus.Pending;
            OrderDate = DateTime.Now;
            CustomerPhone = "";
            CustomerAddress = "";
            Notes = "";
            DiscountAmount = 0;
        }
    }

    public class AuditLog
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Action { get; set; }
        public string EntityType { get; set; }
        public string EntityId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Details { get; set; }
        public string IpAddress { get; set; }

        public AuditLog(string username, string action, string entityType, string entityId, string details = "", string ipAddress = "127.0.0.1")
        {
            Id = Guid.NewGuid().ToString();
            Username = username;
            Action = action;
            EntityType = entityType;
            EntityId = entityId;
            Timestamp = DateTime.Now;
            Details = details;
            IpAddress = ipAddress;
        }
    }

    public class SystemLog
    {
        public string Id { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public string Module { get; set; }
        public DateTime Timestamp { get; set; }
        public string Exception { get; set; }
        public string StackTrace { get; set; }

        public SystemLog(LogLevel level, string message, string module = "", string exception = "", string stackTrace = "")
        {
            Id = Guid.NewGuid().ToString();
            Level = level;
            Message = message;
            Module = module;
            Timestamp = DateTime.Now;
            Exception = exception;
            StackTrace = stackTrace;
        }
    }

    // ==================== SERVICES ====================
    public static class SecurityService
    {
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        public static bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrEmpty(password)) return false;
            if (string.IsNullOrEmpty(hash)) return false;

            try
            {
                return HashPassword(password) == hash;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static string GenerateRandomPassword(int length = 8)
        {
            if (length < 6)
                throw new ArgumentException("Password length must be at least 6", nameof(length));

            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*";
            StringBuilder res = new StringBuilder();
            Random rnd = new Random();

            while (0 < length--)
            {
                res.Append(validChars[rnd.Next(validChars.Length)]);
            }
            return res.ToString();
        }

        public static string GenerateApiKey()
        {
            return Guid.NewGuid().ToString("N");
        }
    }

    public static class Logger
    {
        private static readonly List<SystemLog> logs = new List<SystemLog>();
        private static readonly object lockObject = new object();
        private const int MAX_LOG_SIZE = 10000;

        public static void Info(string message, string module = "")
        {
            Log(LogLevel.INFO, message, module);
        }

        public static void Warning(string message, string module = "")
        {
            Log(LogLevel.WARNING, message, module);
        }

        public static void Error(string message, string module = "", Exception ex = null)
        {
            Log(LogLevel.ERROR, message, module, ex);
        }

        public static void Debug(string message, string module = "")
        {
            Log(LogLevel.DEBUG, message, module);
        }

        private static void Log(LogLevel level, string message, string module = "", Exception ex = null)
        {
            lock (lockObject)
            {
                if (logs.Count >= MAX_LOG_SIZE)
                {
                    logs.RemoveRange(0, 1000); // Remove old logs
                }

                var log = new SystemLog(level, message, module, ex?.Message, ex?.StackTrace);
                logs.Add(log);

                // Console output với màu sắc
                ConsoleColor color = ConsoleColor.White;
                switch (level)
                {
                    case LogLevel.INFO: color = ConsoleColor.Cyan; break;
                    case LogLevel.WARNING: color = ConsoleColor.Yellow; break;
                    case LogLevel.ERROR: color = ConsoleColor.Red; break;
                    case LogLevel.DEBUG: color = ConsoleColor.Gray; break;
                }

                Console.ForegroundColor = color;
                Console.WriteLine($"[{level}] [{DateTime.Now:HH:mm:ss}] {module}: {message}");
                if (ex != null)
                {
                    Console.WriteLine($"Exception: {ex.Message}");
                }
                Console.ResetColor();
            }
        }

        public static List<SystemLog> GetLogs(LogLevel? level = null, string module = "", int count = 100)
        {
            var query = logs.AsEnumerable();
            if (level.HasValue) query = query.Where(l => l.Level == level.Value);
            if (!string.IsNullOrEmpty(module)) query = query.Where(l => l.Module.Contains(module));
            return query.OrderByDescending(l => l.Timestamp).Take(count).ToList();
        }

        public static void ExportLogs(string filePath)
        {
            try
            {
                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("Timestamp,Level,Module,Message,Exception");
                    foreach (var log in logs.OrderBy(l => l.Timestamp))
                    {
                        writer.WriteLine($"{log.Timestamp:yyyy-MM-dd HH:mm:ss},{log.Level},{log.Module},\"{log.Message}\",\"{log.Exception}\"");
                    }
                }
                Info($"Logs exported to {filePath}", "Logger");
            }
            catch (Exception ex)
            {
                Error($"Failed to export logs: {ex.Message}", "Logger", ex);
            }
        }

        public static void ClearOldLogs(int daysToKeep = 7)
        {
            var cutoff = DateTime.Now.AddDays(-daysToKeep);
            logs.RemoveAll(l => l.Timestamp < cutoff);
            Info($"Cleared logs older than {daysToKeep} days", "Logger");
        }
    }

    public interface ICommand
    {
        void Execute();
        void Undo();
        string Description { get; }
    }

    public class UndoRedoService
    {
        private Stack<ICommand> undoStack = new Stack<ICommand>();
        private Stack<ICommand> redoStack = new Stack<ICommand>();
        private readonly int maxStackSize = 50;

        public event Action<string> OnCommandExecuted;
        public event Action<string> OnCommandUndone;
        public event Action<string> OnCommandRedone;

        public void ExecuteCommand(ICommand command)
        {
            try
            {
                command.Execute();
                undoStack.Push(command);

                // Giới hạn kích thước stack
                if (undoStack.Count > maxStackSize)
                {
                    var oldStack = new Stack<ICommand>();
                    while (undoStack.Count > maxStackSize / 2)
                    {
                        oldStack.Push(undoStack.Pop());
                    }
                    undoStack.Clear();
                    while (oldStack.Count > 0)
                    {
                        undoStack.Push(oldStack.Pop());
                    }
                }

                redoStack.Clear();
                OnCommandExecuted?.Invoke(command.Description);
                Logger.Info($"Command executed: {command.Description}", "UndoRedo");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to execute command: {command.Description}", "UndoRedo", ex);
                throw;
            }
        }

        public void Undo()
        {
            if (undoStack.Count > 0)
            {
                ICommand command = undoStack.Pop();
                command.Undo();
                redoStack.Push(command);
                OnCommandUndone?.Invoke(command.Description);
                Logger.Info($"Command undone: {command.Description}", "UndoRedo");
            }
        }

        public void Redo()
        {
            if (redoStack.Count > 0)
            {
                ICommand command = redoStack.Pop();
                command.Execute();
                undoStack.Push(command);
                OnCommandRedone?.Invoke(command.Description);
                Logger.Info($"Command redone: {command.Description}", "UndoRedo");
            }
        }

        public void Clear()
        {
            undoStack.Clear();
            redoStack.Clear();
            Logger.Info("Undo/Redo history cleared", "UndoRedo");
        }

        public List<string> GetUndoHistory()
        {
            return undoStack.Select(c => c.Description).ToList();
        }

        public List<string> GetRedoHistory()
        {
            return redoStack.Select(c => c.Description).ToList();
        }

        public bool CanUndo { get { return undoStack.Count > 0; } }
        public bool CanRedo { get { return redoStack.Count > 0; } }
        public int UndoCount { get { return undoStack.Count; } }
        public int RedoCount { get { return redoStack.Count; } }
    }

    // ==================== COMMAND PATTERN IMPLEMENTATIONS ====================
    public class AddDishCommand : ICommand
    {
        private RestaurantSystem system;
        private Dish dish;
        public string Description => $"Thêm món ăn: {dish.Name}";

        public AddDishCommand(RestaurantSystem system, Dish dish)
        {
            this.system = system ?? throw new ArgumentNullException(nameof(system));
            this.dish = dish ?? throw new ArgumentNullException(nameof(dish));
        }

        public void Execute()
        {
            system.GetRepository().Dishes[dish.Id] = dish;
        }

        public void Undo()
        {
            system.GetRepository().Dishes.Remove(dish.Id);
        }
    }

    public class UpdateDishCommand : ICommand
    {
        private RestaurantSystem system;
        private Dish oldDish;
        private Dish newDish;
        public string Description => $"Cập nhật món ăn: {newDish.Name}";

        public UpdateDishCommand(RestaurantSystem system, Dish oldDish, Dish newDish)
        {
            this.system = system ?? throw new ArgumentNullException(nameof(system));
            this.oldDish = oldDish ?? throw new ArgumentNullException(nameof(oldDish));
            this.newDish = newDish ?? throw new ArgumentNullException(nameof(newDish));
        }

        public void Execute()
        {
            system.GetRepository().Dishes[newDish.Id] = newDish;
        }

        public void Undo()
        {
            system.GetRepository().Dishes[oldDish.Id] = oldDish;
        }
    }

    public class DeleteDishCommand : ICommand
    {
        private RestaurantSystem system;
        private Dish dish;
        public string Description => $"Xóa món ăn: {dish.Name}";

        public DeleteDishCommand(RestaurantSystem system, Dish dish)
        {
            this.system = system ?? throw new ArgumentNullException(nameof(system));
            this.dish = dish ?? throw new ArgumentNullException(nameof(dish));
        }

        public void Execute()
        {
            system.GetRepository().Dishes.Remove(dish.Id);
        }

        public void Undo()
        {
            system.GetRepository().Dishes[dish.Id] = dish;
        }
    }

    public class AddIngredientCommand : ICommand
    {
        private RestaurantSystem system;
        private Ingredient ingredient;
        public string Description => $"Thêm nguyên liệu: {ingredient.Name}";

        public AddIngredientCommand(RestaurantSystem system, Ingredient ingredient)
        {
            this.system = system ?? throw new ArgumentNullException(nameof(system));
            this.ingredient = ingredient ?? throw new ArgumentNullException(nameof(ingredient));
        }

        public void Execute()
        {
            system.GetRepository().Ingredients[ingredient.Id] = ingredient;
        }

        public void Undo()
        {
            system.GetRepository().Ingredients.Remove(ingredient.Id);
        }
    }

    public class UpdateIngredientCommand : ICommand
    {
        private RestaurantSystem system;
        private Ingredient oldIngredient;
        private Ingredient newIngredient;
        public string Description => $"Cập nhật nguyên liệu: {newIngredient.Name}";

        public UpdateIngredientCommand(RestaurantSystem system, Ingredient oldIngredient, Ingredient newIngredient)
        {
            this.system = system ?? throw new ArgumentNullException(nameof(system));
            this.oldIngredient = oldIngredient ?? throw new ArgumentNullException(nameof(oldIngredient));
            this.newIngredient = newIngredient ?? throw new ArgumentNullException(nameof(newIngredient));
        }

        public void Execute()
        {
            system.GetRepository().Ingredients[newIngredient.Id] = newIngredient;
        }

        public void Undo()
        {
            system.GetRepository().Ingredients[oldIngredient.Id] = oldIngredient;
        }
    }

    public class DeleteIngredientCommand : ICommand
    {
        private RestaurantSystem system;
        private Ingredient ingredient;
        public string Description => $"Xóa nguyên liệu: {ingredient.Name}";

        public DeleteIngredientCommand(RestaurantSystem system, Ingredient ingredient)
        {
            this.system = system ?? throw new ArgumentNullException(nameof(system));
            this.ingredient = ingredient ?? throw new ArgumentNullException(nameof(ingredient));
        }

        public void Execute()
        {
            system.GetRepository().Ingredients.Remove(ingredient.Id);
        }

        public void Undo()
        {
            system.GetRepository().Ingredients[ingredient.Id] = ingredient;
        }
    }

    public class BatchAddDishesCommand : ICommand
    {
        private RestaurantSystem system;
        private List<Dish> dishes;
        public string Description => $"Thêm hàng loạt {dishes.Count} món ăn";

        public BatchAddDishesCommand(RestaurantSystem system, List<Dish> dishes)
        {
            this.system = system;
            this.dishes = dishes;
        }

        public void Execute()
        {
            foreach (var dish in dishes)
            {
                system.GetRepository().Dishes[dish.Id] = dish;
            }
        }

        public void Undo()
        {
            foreach (var dish in dishes)
            {
                system.GetRepository().Dishes.Remove(dish.Id);
            }
        }
    }

    public class BatchUpdateDishesCommand : ICommand
    {
        private RestaurantSystem system;
        private List<Dish> oldDishes;
        private List<Dish> newDishes;
        public string Description => $"Cập nhật hàng loạt {newDishes.Count} món ăn";

        public BatchUpdateDishesCommand(RestaurantSystem system, List<Dish> oldDishes, List<Dish> newDishes)
        {
            this.system = system;
            this.oldDishes = oldDishes;
            this.newDishes = newDishes;
        }

        public void Execute()
        {
            foreach (var dish in newDishes)
            {
                system.GetRepository().Dishes[dish.Id] = dish;
            }
        }

        public void Undo()
        {
            foreach (var dish in oldDishes)
            {
                system.GetRepository().Dishes[dish.Id] = dish;
            }
        }
    }

    public class BatchDeleteDishesCommand : ICommand
    {
        private RestaurantSystem system;
        private List<Dish> dishes;
        public string Description => $"Xóa hàng loạt {dishes.Count} món ăn";

        public BatchDeleteDishesCommand(RestaurantSystem system, List<Dish> dishes)
        {
            this.system = system;
            this.dishes = dishes;
        }

        public void Execute()
        {
            foreach (var dish in dishes)
            {
                system.GetRepository().Dishes.Remove(dish.Id);
            }
        }

        public void Undo()
        {
            foreach (var dish in dishes)
            {
                system.GetRepository().Dishes[dish.Id] = dish;
            }
        }
    }

    public class CreateOrderCommand : ICommand
    {
        private RestaurantSystem system;
        private Order order;
        private Dictionary<string, decimal> originalQuantities;
        public string Description => $"Tạo đơn hàng: {order.Id}";

        public CreateOrderCommand(RestaurantSystem system, Order order)
        {
            this.system = system;
            this.order = order;
            this.originalQuantities = new Dictionary<string, decimal>();
        }

        public void Execute()
        {
            // Lưu số lượng nguyên liệu gốc
            foreach (var item in order.Items)
            {
                if (!item.IsCombo)
                {
                    var dish = system.GetRepository().Dishes[item.ItemId];
                    foreach (var ing in dish.Ingredients)
                    {
                        if (!originalQuantities.ContainsKey(ing.Key))
                        {
                            originalQuantities[ing.Key] = system.GetRepository().Ingredients[ing.Key].Quantity;
                        }
                    }
                }
                else
                {
                    var combo = system.GetRepository().Combos[item.ItemId];
                    foreach (var dishId in combo.DishIds)
                    {
                        var dish = system.GetRepository().Dishes[dishId];
                        foreach (var ing in dish.Ingredients)
                        {
                            if (!originalQuantities.ContainsKey(ing.Key))
                            {
                                originalQuantities[ing.Key] = system.GetRepository().Ingredients[ing.Key].Quantity;
                            }
                        }
                    }
                }
            }

            // Trừ nguyên liệu
            if (system.DeductIngredients(order))
            {
                system.GetRepository().Orders[order.Id] = order;

                // Cập nhật số lượt bán
                foreach (var item in order.Items)
                {
                    if (!item.IsCombo)
                    {
                        if (system.GetRepository().Dishes.ContainsKey(item.ItemId))
                        {
                            system.GetRepository().Dishes[item.ItemId].SalesCount += item.Quantity;
                        }
                    }
                    else
                    {
                        if (system.GetRepository().Combos.ContainsKey(item.ItemId))
                        {
                            system.GetRepository().Combos[item.ItemId].SalesCount += item.Quantity;
                        }
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("Không đủ nguyên liệu để thực hiện đơn hàng");
            }
        }
        public void Undo()
        {
            // Khôi phục số lượng nguyên liệu
            foreach (var kvp in originalQuantities)
            {
                system.GetRepository().Ingredients[kvp.Key].Quantity = kvp.Value;
            }
            // Khôi phục số lượt bán
            foreach (var item in order.Items)
            {
                if (!item.IsCombo)
                {
                    if (system.GetRepository().Dishes.ContainsKey(item.ItemId))
                    {
                        system.GetRepository().Dishes[item.ItemId].SalesCount -= item.Quantity;
                    }
                }
                else
                {
                    if (system.GetRepository().Combos.ContainsKey(item.ItemId))
                    {
                        system.GetRepository().Combos[item.ItemId].SalesCount -= item.Quantity;
                    }
                }
            }
            system.GetRepository().Orders.Remove(order.Id);
        }
    }
    public class UpdateOrderStatusCommand : ICommand
    {
        private RestaurantSystem system;
        private Order order;
        private OrderStatus oldStatus;
        private OrderStatus newStatus;
        public string Description => $"Cập nhật trạng thái đơn hàng {order.Id}: {oldStatus} -> {newStatus}";
        public UpdateOrderStatusCommand(RestaurantSystem system, Order order, OrderStatus newStatus)
        {
            this.system = system;
            this.order = order;
            this.oldStatus = order.Status;
            this.newStatus = newStatus;
        }
        public void Execute()
        {
            order.Status = newStatus;
            if (newStatus == OrderStatus.Completed)
            {
                order.CompletedDate = DateTime.Now;
            }
        }

        public void Undo()
        {
            order.Status = oldStatus;
            if (oldStatus != OrderStatus.Completed)
            {
                order.CompletedDate = null;
            }
        }
    }

    // ==================== DATA REPOSITORY ====================
    public class DataRepository
    {
        public Dictionary<string, User> Users { get; set; }
        public Dictionary<string, Ingredient> Ingredients { get; set; }
        public Dictionary<string, Dish> Dishes { get; set; }
        public Dictionary<string, Combo> Combos { get; set; }
        public Dictionary<string, Order> Orders { get; set; }
        public List<AuditLog> AuditLogs { get; set; }

        public DataRepository()
        {
            Users = new Dictionary<string, User>();
            Ingredients = new Dictionary<string, Ingredient>();
            Dishes = new Dictionary<string, Dish>();
            Combos = new Dictionary<string, Combo>();
            Orders = new Dictionary<string, Order>();
            AuditLogs = new List<AuditLog>();
        }

        public DataRepository(Dictionary<string, User> users, Dictionary<string, Ingredient> ingredients,
                            Dictionary<string, Dish> dishes, Dictionary<string, Combo> combos,
                            Dictionary<string, Order> orders, List<AuditLog> auditLogs)
        {
            Users = users;
            Ingredients = ingredients;
            Dishes = dishes;
            Combos = combos;
            Orders = orders;
            AuditLogs = auditLogs;
        }
    }


    // ==================== MEMORY MANAGEMENT ====================
    // ==================== COMPLETE MEMORYMANAGER CLASS ====================
    public class MemoryManager
    {
        private RestaurantSystem system;
        private const int CLEANUP_INTERVAL = 300000;

        public MemoryManager(RestaurantSystem system)
        {
            this.system = system ?? throw new ArgumentNullException(nameof(system));
        }

        public void Cleanup(object state = null)
        {
            try
            {
                Logger.Info("Starting memory cleanup", "MemoryManager");

                // Dọn dẹp logs cũ
                Logger.ClearOldLogs(7);

                // Dọn dẹp thư mục tạm
                CleanupTempFiles();

                // Thu gom bộ nhớ
                GC.Collect();
                GC.WaitForPendingFinalizers();

                Logger.Info("Memory cleanup completed", "MemoryManager");
            }
            catch (Exception ex)
            {
                Logger.Error("Memory cleanup failed", "MemoryManager", ex);
            }
        }

        private void CleanupTempFiles()
        {
            try
            {
                string tempDir = Path.GetTempPath();
                var tempFiles = Directory.GetFiles(tempDir, "rms_*.tmp")
                                       .Where(f => File.GetCreationTime(f) < DateTime.Now.AddDays(-1))
                                       .ToList();

                foreach (var tempFile in tempFiles)
                {
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch
                    {
                        // Bỏ qua nếu không xóa được
                    }
                }

                if (tempFiles.Count > 0)
                {
                    Logger.Info($"Cleaned up {tempFiles.Count} temp files", "MemoryManager");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Temp file cleanup failed", "MemoryManager", ex);
            }
        }

        public string GetMemoryInfo()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var memoryMB = process.WorkingSet64 / 1024 / 1024;
                var memoryMaxMB = process.PeakWorkingSet64 / 1024 / 1024;

                var repo = system.GetRepository();
                return $"Bộ nhớ sử dụng: {memoryMB} MB (Max: {memoryMaxMB} MB)\n" +
                       $"Số lượng: {repo.Dishes.Count} món, " +
                       $"{repo.Ingredients.Count} nguyên liệu, " +
                       $"{repo.Orders.Count} đơn hàng";
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get memory info", "MemoryManager", ex);
                return "Không thể lấy thông tin bộ nhớ";
            }
        }

        // ==================== OPTIMIZE LARGE DATASETS METHOD ====================
        public void OptimizeLargeDatasets()
        {
            Logger.Info("Optimizing large datasets", "MemoryManager");
            try
            {
                var repo = system.GetRepository();
                // Tối ưu dictionary bằng việc set cap
                if (repo.Dishes.Count > 1000)
                {
                    var newDishes = new Dictionary<string, Dish>(repo.Dishes.Count);
                    foreach (var kvp in repo.Dishes)
                        newDishes[kvp.Key] = kvp.Value;
                    repo.Dishes = newDishes;
                    Logger.Info($"Optimized dishes dictionary: {repo.Dishes.Count} items", "MemoryManager");
                }
                if (repo.Ingredients.Count > 1000)
                {
                    var newIngredients = new Dictionary<string, Ingredient>(repo.Ingredients.Count);
                    foreach (var kvp in repo.Ingredients)
                        newIngredients[kvp.Key] = kvp.Value;
                    repo.Ingredients = newIngredients;
                    Logger.Info($"Optimized ingredients dictionary: {repo.Ingredients.Count} items", "MemoryManager");
                }

                if (repo.Orders.Count > 5000)
                {
                    // Lưu orders cũ ra file và xóa khỏi memory
                    ArchiveOldOrders();
                }

                if (repo.AuditLogs.Count > 10000)
                {
                    // Giữ lại chỉ 5000 logs gần nhất
                    var recentLogs = repo.AuditLogs
                        .OrderByDescending(a => a.Timestamp)
                        .Take(5000)
                        .ToList();
                    repo.AuditLogs = recentLogs;
                    Logger.Info($"Optimized audit logs: {repo.AuditLogs.Count} items", "MemoryManager");
                }

                // Thu gom bộ nhớ sau khi tối ưu
                GC.Collect();
                GC.WaitForPendingFinalizers();

                Logger.Info("Dataset optimization completed", "MemoryManager");
            }
            catch (Exception ex)
            {
                Logger.Error("Dataset optimization failed", "MemoryManager", ex);
                throw;
            }
        }

        private void ArchiveOldOrders()
        {
            try
            {
                var repo = system.GetRepository();
                var oldOrders = repo.Orders
                    .Where(o => o.Value.OrderDate < DateTime.Now.AddMonths(-3))
                    .ToList();

                if (oldOrders.Any())
                {
                    string archivePath = Path.Combine("Data", "Archives");
                    if (!Directory.Exists(archivePath))
                        Directory.CreateDirectory(archivePath);

                    string archiveFile = Path.Combine(archivePath, $"orders_archive_{DateTime.Now:yyyyMMddHHmmss}.json");

                    var archiveData = oldOrders.ToDictionary(o => o.Key, o => o.Value);
                    string json = JsonConvert.SerializeObject(archiveData, Formatting.Indented);
                    File.WriteAllText(archiveFile, json);

                    foreach (var order in oldOrders)
                    {
                        repo.Orders.Remove(order.Key);
                    }

                    Logger.Info($"Archived {oldOrders.Count} old orders to {archiveFile}", "MemoryManager");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to archive old orders", "MemoryManager", ex);
            }
        }

        // ==================== PERFORMANCE OPTIMIZATION METHODS ====================
        public void CompactData()
        {
            try
            {
                Logger.Info("Compacting data structures", "MemoryManager");

                var repo = system.GetRepository();

                // Chuyển đổi sang dictionaries với capacity tối ưu
                repo.Dishes = new Dictionary<string, Dish>(repo.Dishes);
                repo.Ingredients = new Dictionary<string, Ingredient>(repo.Ingredients);
                repo.Combos = new Dictionary<string, Combo>(repo.Combos);
                repo.Orders = new Dictionary<string, Order>(repo.Orders);

                // Sắp xếp logs để tối ưu truy cập
                repo.AuditLogs = repo.AuditLogs
                    .OrderByDescending(a => a.Timestamp)
                    .ToList();

                GC.Collect();
                GC.WaitForPendingFinalizers();

                Logger.Info("Data compaction completed", "MemoryManager");
            }
            catch (Exception ex)
            {
                Logger.Error("Data compaction failed", "MemoryManager", ex);
            }
        }

        public string GetPerformanceStats()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var memoryMB = process.WorkingSet64 / 1024 / 1024;
                var handleCount = process.HandleCount;
                var threads = process.Threads.Count;

                var repo = system.GetRepository();
                var stats = new StringBuilder();

                stats.AppendLine("📊 THỐNG KÊ HIỆU SUẤT:");
                stats.AppendLine($"• Bộ nhớ sử dụng: {memoryMB} MB");
                stats.AppendLine($"• Số handles: {handleCount}");
                stats.AppendLine($"• Số threads: {threads}");
                stats.AppendLine($"• Món ăn: {repo.Dishes.Count}");
                stats.AppendLine($"• Nguyên liệu: {repo.Ingredients.Count}");
                stats.AppendLine($"• Combo: {repo.Combos.Count}");
                stats.AppendLine($"• Đơn hàng: {repo.Orders.Count}");
                stats.AppendLine($"• Audit logs: {repo.AuditLogs.Count}");

                // Tính tổng kích thước ước tính
                long estimatedSize = (repo.Dishes.Count * 500) + (repo.Ingredients.Count * 300) +
                                   (repo.Combos.Count * 400) + (repo.Orders.Count * 1000) +
                                   (repo.AuditLogs.Count * 200);
                stats.AppendLine($"• Kích thước ước tính: {estimatedSize / 1024} KB");

                return stats.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get performance stats", "MemoryManager", ex);
                return "Không thể lấy thông tin hiệu suất";
            }
        }
    }



    // ==================== UNIT TESTS ====================
    public static class UnitTests
    {
        public static void RunAllTests()
        {
            Logger.Info("Running unit tests", "UnitTests");

            try
            {
                TestSecurityService();
                TestCommandPattern();
                TestMemoryManagement();
                TestBusinessLogic();

                Logger.Info("All unit tests completed", "UnitTests");
                EnhancedUI.DisplaySuccess("Tất cả unit tests đã PASSED!");
            }
            catch (Exception ex)
            {
                Logger.Error("Unit tests failed", "UnitTests", ex);
                EnhancedUI.DisplayError($"Unit tests FAILED: {ex.Message}");
                throw;
            }
        }

        public static void RunComprehensiveTests()
        {
            Logger.Info("Running comprehensive tests...", "UnitTests");

            try
            {
                // Test 1: Security Service
                TestSecurityService();

                // Test 2: Command Pattern
                TestCommandPattern();

                // Test 3: Memory Management
                TestMemoryManagement();

                // Test 4: Business Logic
                TestBusinessLogic();

                // Test 5: Integration Test
                TestIntegration();

                Logger.Info("All comprehensive tests PASSED!", "UnitTests");
                EnhancedUI.DisplaySuccess("🎉 Tất cả comprehensive tests đã PASSED!");
            }
            catch (Exception ex)
            {
                Logger.Error("Comprehensive tests failed", "UnitTests", ex);
                EnhancedUI.DisplayError($"Comprehensive tests FAILED: {ex.Message}");
                throw;
            }
        }

        // CHUYỂN TẤT CẢ PHƯƠNG THỨC TEST SANG PUBLIC
        public static void TestSecurityService()
        {
            Logger.Info("Testing SecurityService...", "UnitTests");

            // Test 1: Normal password hashing and verification
            string password = "test123";
            string hash = SecurityService.HashPassword(password);
            bool verified = SecurityService.VerifyPassword(password, hash);

            if (!verified)
                throw new Exception("Password verification failed");

            // Test 2: Random password generation
            string randomPass = SecurityService.GenerateRandomPassword();
            if (string.IsNullOrEmpty(randomPass) || randomPass.Length != 8)
                throw new Exception("Random password generation failed");

            // Test 3: Empty password should throw exception
            try
            {
                SecurityService.HashPassword("");
                throw new Exception("Should have thrown exception for empty password");
            }
            catch (ArgumentException)
            {
                Logger.Info("✓ Empty password correctly throws exception", "UnitTests");
            }

            // Test 4: Null password should throw exception
            try
            {
                SecurityService.HashPassword(null);
                throw new Exception("Should have thrown exception for null password");
            }
            catch (ArgumentException)
            {
                Logger.Info("✓ Null password correctly throws exception", "UnitTests");
            }

            // Test 5: Short password length should throw exception
            try
            {
                SecurityService.GenerateRandomPassword(5);
                throw new Exception("Should have thrown exception for short password length");
            }
            catch (ArgumentException)
            {
                Logger.Info("✓ Short password length correctly throws exception", "UnitTests");
            }

            // Test 6: VerifyPassword with empty inputs should return false
            if (SecurityService.VerifyPassword("", "somehash"))
                throw new Exception("VerifyPassword should return false for empty password");

            if (SecurityService.VerifyPassword("password", ""))
                throw new Exception("VerifyPassword should return false for empty hash");

            if (SecurityService.VerifyPassword("", ""))
                throw new Exception("VerifyPassword should return false for both empty");

            Logger.Info("SecurityService tests PASSED", "UnitTests");
        }

        public static void TestCommandPattern()
        {
            Logger.Info("Testing CommandPattern...", "UnitTests");

            var system = new RestaurantSystem();
            var undoRedo = new UndoRedoService();

            // Test AddDishCommand
            var dish = new Dish("TEST001", "Test Dish", "Test Description", 50000, "Test Category");
            var addCommand = new AddDishCommand(system, dish);

            undoRedo.ExecuteCommand(addCommand);
            if (!system.GetRepository().Dishes.ContainsKey("TEST001"))
                throw new Exception("AddDishCommand execute failed");

            undoRedo.Undo();
            if (system.GetRepository().Dishes.ContainsKey("TEST001"))
                throw new Exception("AddDishCommand undo failed");

            undoRedo.Redo();
            if (!system.GetRepository().Dishes.ContainsKey("TEST001"))
                throw new Exception("AddDishCommand redo failed");

            // Test null safety - SYSTEM
            try
            {
                new AddDishCommand(null, dish);
                throw new Exception("Should have thrown exception for null system");
            }
            catch (ArgumentNullException ex) when (ex.ParamName == "system")
            {
                Logger.Info("✓ Null system correctly throws exception", "UnitTests");
            }
            catch (Exception)
            {
                throw new Exception("Wrong exception type thrown for null system");
            }

            // Test null safety - DISH
            try
            {
                new AddDishCommand(system, null);
                throw new Exception("Should have thrown exception for null dish");
            }
            catch (ArgumentNullException ex) when (ex.ParamName == "dish")
            {
                Logger.Info("✓ Null dish correctly throws exception", "UnitTests");
            }
            catch (Exception)
            {
                throw new Exception("Wrong exception type thrown for null dish");
            }

            // Test other commands for null safety
            try
            {
                new UpdateDishCommand(null, dish, dish);
                throw new Exception("Should have thrown exception for null system in UpdateDishCommand");
            }
            catch (ArgumentNullException)
            {
                Logger.Info("✓ UpdateDishCommand null system correctly throws exception", "UnitTests");
            }

            try
            {
                new DeleteIngredientCommand(system, null);
                throw new Exception("Should have thrown exception for null ingredient in DeleteIngredientCommand");
            }
            catch (ArgumentNullException)
            {
                Logger.Info("✓ DeleteIngredientCommand null ingredient correctly throws exception", "UnitTests");
            }

            Logger.Info("CommandPattern tests PASSED", "UnitTests");
        }

        public static void TestMemoryManagement()
        {
            Logger.Info("Testing MemoryManagement...", "UnitTests");

            var system = new RestaurantSystem();
            var memoryManager = new MemoryManager(system);

            string memoryInfo = memoryManager.GetMemoryInfo();
            if (string.IsNullOrEmpty(memoryInfo))
                throw new Exception("Memory info generation failed");

            memoryManager.Cleanup();

            // Test with null system - FIXED: Should throw exception
            try
            {
                new MemoryManager(null);
                throw new Exception("Should have thrown exception for null system");
            }
            catch (ArgumentNullException ex) when (ex.ParamName == "system")
            {
                Logger.Info("✓ Null system correctly throws exception in MemoryManager", "UnitTests");
            }
            catch (Exception ex)
            {
                throw new Exception($"Wrong exception type thrown for null system: {ex.GetType().Name}");
            }

            Logger.Info("MemoryManagement tests PASSED", "UnitTests");
        }

        public static void TestBusinessLogicWithCleanEnvironment()
        {
            Logger.Info("Testing BusinessLogic with clean environment...", "UnitTests");

            // Tạo repository hoàn toàn mới để tránh conflict
            var repo = new DataRepository();

            // Test ingredient creation
            var ingredient = new Ingredient("TEST_ING", "Test Ingredient", "kg", 10, 2, 50000);
            repo.Ingredients["TEST_ING"] = ingredient;

            // Test dish creation - THÊM VÀO REPO NGAY LẬP TỨC
            var dish = new Dish("TEST_DISH", "Test Dish", "Test Description", 100000, "Test Category");
            dish.Ingredients["TEST_ING"] = 0.5m;
            repo.Dishes["TEST_DISH"] = dish; // QUAN TRỌNG: THÊM VÀO REPO TRƯỚC

            // Calculate cost
            dish.CalculateCost(repo.Ingredients);

            // Test combo creation
            var combo = new Combo("TEST_COMBO", "Test Combo", "Test Description", 10);
            combo.DishIds.Add("TEST_DISH");

            // Calculate combo prices
            combo.CalculateOriginalPrice(repo.Dishes);
            combo.CalculateCost(repo.Dishes);

            // Verify
            if (combo.OriginalPrice != 100000)
                throw new Exception($"Clean env combo price failed. Expected 100000, got {combo.OriginalPrice}");

            Logger.Info("Clean environment BusinessLogic tests PASSED", "UnitTests");
        }

        public static void TestBusinessLogic()
        {
            Logger.Info("Testing BusinessLogic...", "UnitTests");

            // Create a clean system for testing
            var system = new RestaurantSystem();
            var repo = system.GetRepository();

            // Clear any existing test data to avoid conflicts
            repo.Ingredients.Remove("TEST_ING");
            repo.Dishes.Remove("TEST_DISH");
            repo.Combos.Remove("TEST_COMBO");

            // Test ingredient creation and validation
            var ingredient = new Ingredient("TEST_ING", "Test Ingredient", "kg", 10, 2, 50000);
            repo.Ingredients["TEST_ING"] = ingredient;
            Logger.Info($"Created ingredient: {ingredient.Name}", "UnitTests");

            // Test dish creation and cost calculation - QUAN TRỌNG: THÊM VÀO REPOSITORY TRƯỚC
            var dish = new Dish("TEST_DISH", "Test Dish", "Test Description", 100000, "Test Category");
            dish.Ingredients["TEST_ING"] = 0.5m;

            // THÊM DISH VÀO REPOSITORY TRƯỚC KHI TÍNH COST
            repo.Dishes["TEST_DISH"] = dish;
            Logger.Info($"Created and added dish to repo: {dish.Name} - {dish.Price:N0}đ", "UnitTests");

            // Sau đó mới tính cost
            decimal cost = dish.CalculateCost(repo.Ingredients);
            decimal expectedCost = 25000; // 0.5 * 50000
            if (cost != expectedCost)
                throw new Exception($"Cost calculation failed. Expected {expectedCost}, got {cost}");

            Logger.Info($"Dish cost calculated: {cost:N0}đ", "UnitTests");

            // Test combo creation and price calculation
            var combo = new Combo("TEST_COMBO", "Test Combo", "Test Combo Description", 10);

            // Add dish to combo first - DISH ĐÃ CÓ TRONG REPOSITORY
            combo.DishIds.Add("TEST_DISH");
            Logger.Info($"Added dish to combo. Dish exists in repo: {repo.Dishes.ContainsKey("TEST_DISH")}", "UnitTests");

            // THEN calculate prices - ĐẢM BẢO DISH ĐÃ CÓ TRONG REPOSITORY
            combo.CalculateOriginalPrice(repo.Dishes);
            combo.CalculateCost(repo.Dishes);

            decimal expectedOriginalPrice = 100000; // dish price
            decimal expectedFinalPrice = 90000; // 100000 * (1 - 0.10)

            if (combo.OriginalPrice != expectedOriginalPrice)
            {
                Logger.Error($"Combo calculation details - Dishes in repo: {repo.Dishes.Count}, Looking for: TEST_DISH, Found: {repo.Dishes.ContainsKey("TEST_DISH")}", "UnitTests");
                if (repo.Dishes.ContainsKey("TEST_DISH"))
                {
                    var actualDish = repo.Dishes["TEST_DISH"];
                    Logger.Error($"Dish details - Name: {actualDish.Name}, Price: {actualDish.Price:N0}đ", "UnitTests");
                }
                throw new Exception($"Combo price calculation failed. Expected {expectedOriginalPrice}, got {combo.OriginalPrice}");
            }

            if (combo.FinalPrice != expectedFinalPrice)
                throw new Exception($"Combo discount calculation failed. Expected {expectedFinalPrice}, got {combo.FinalPrice}");

            // Test profit margin calculation
            decimal expectedProfitMargin = (expectedFinalPrice - 25000) / expectedFinalPrice * 100;
            if (Math.Abs(combo.ProfitMargin - expectedProfitMargin) > 0.1m)
                throw new Exception($"Profit margin calculation failed. Expected {expectedProfitMargin:F2}, got {combo.ProfitMargin:F2}");

            Logger.Info("BusinessLogic tests PASSED", "UnitTests");
        }

        public static void TestIntegration()
        {
            Logger.Info("Testing Integration...", "UnitTests");

            // Test the complete flow: Ingredient -> Dish -> Combo -> Order
            var system = new RestaurantSystem();
            var repo = system.GetRepository();
            var undoRedo = new UndoRedoService();

            // Clean test data
            repo.Ingredients.Remove("INTEG_TEST_ING");
            repo.Dishes.Remove("INTEG_TEST_DISH");
            repo.Combos.Remove("INTEG_TEST_COMBO");
            repo.Orders.Remove("INTEG_TEST_ORDER");

            // Create ingredient
            var ingredient = new Ingredient("INTEG_TEST_ING", "Integration Test Ingredient", "kg", 100, 10, 10000);
            repo.Ingredients["INTEG_TEST_ING"] = ingredient;

            // Create dish
            var dish = new Dish("INTEG_TEST_DISH", "Integration Test Dish", "Integration Test", 50000, "Test");
            dish.Ingredients["INTEG_TEST_ING"] = 1m; // 1kg
            dish.CalculateCost(repo.Ingredients);
            repo.Dishes["INTEG_TEST_DISH"] = dish;

            // Create combo
            var combo = new Combo("INTEG_TEST_COMBO", "Integration Test Combo", "Integration Test", 20);
            combo.DishIds.Add("INTEG_TEST_DISH");
            combo.CalculateOriginalPrice(repo.Dishes);
            combo.CalculateCost(repo.Dishes);
            repo.Combos["INTEG_TEST_COMBO"] = combo;

            // Verify calculations
            if (dish.Cost != 10000) throw new Exception("Dish cost integration test failed");
            if (combo.OriginalPrice != 50000) throw new Exception("Combo original price integration test failed");
            if (combo.FinalPrice != 40000) throw new Exception("Combo final price integration test failed");

            // Test order creation with command
            var order = new Order("INTEG_TEST_ORDER", "Integration Test Customer", "testuser");
            var orderItem = new OrderItem
            {
                ItemId = "INTEG_TEST_DISH",
                IsCombo = false,
                Quantity = 2,
                UnitPrice = dish.Price,
                ItemName = dish.Name
            };
            order.Items.Add(orderItem);

            var orderCommand = new CreateOrderCommand(system, order);

            // Store original quantity for verification
            decimal originalQuantity = repo.Ingredients["INTEG_TEST_ING"].Quantity;

            undoRedo.ExecuteCommand(orderCommand);

            // Verify ingredient deduction
            decimal expectedQuantityAfterOrder = originalQuantity - (1m * 2); // 1kg per dish * 2 quantity
            if (repo.Ingredients["INTEG_TEST_ING"].Quantity != expectedQuantityAfterOrder)
                throw new Exception("Ingredient deduction integration test failed");

            // Verify sales count
            if (repo.Dishes["INTEG_TEST_DISH"].SalesCount != 2)
                throw new Exception("Sales count integration test failed");

            // Test undo
            undoRedo.Undo();

            // Verify undo worked
            if (repo.Ingredients["INTEG_TEST_ING"].Quantity != originalQuantity)
                throw new Exception("Undo integration test failed");

            if (repo.Dishes["INTEG_TEST_DISH"].SalesCount != 0)
                throw new Exception("Sales count undo integration test failed");

            if (repo.Orders.ContainsKey("INTEG_TEST_ORDER"))
                throw new Exception("Order removal undo integration test failed");

            Logger.Info("Integration tests PASSED", "UnitTests");
        }
    }

    // ==================== ENHANCED UI COMPONENTS ====================
    public static class EnhancedUI
    {
        public static void DisplayHeader(string title)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║{CenterText(title, 64)}║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
        }

        public static string CenterText(string text, int width)
        {
            if (text.Length >= width) return text;
            int padding = (width - text.Length) / 2;
            return text.PadLeft(padding + text.Length).PadRight(width);
        }

        public static void DisplaySuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ {message}");
            Console.ResetColor();
        }

        public static void DisplayError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ {message}");
            Console.ResetColor();
        }

        public static void DisplayWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️ {message}");
            Console.ResetColor();
        }

        public static void DisplayInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"ℹ️ {message}");
            Console.ResetColor();
        }

        public static string ReadPassword(string prompt = "Mật khẩu: ")
        {
            Console.Write(prompt);
            string password = "";
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                    Console.Write("\b \b");
                }
                else if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
            } while (key.Key != ConsoleKey.Enter);

            Console.WriteLine();
            return password;
        }

        public static int ShowMenu(string title, List<string> options, bool showExit = true)
        {
            DisplayHeader(title);

            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            for (int i = 0; i < options.Count; i++)
            {
                Console.WriteLine($"║ {i + 1,2}. {options[i],-58} ║");
            }
            if (showExit)
            {
                Console.WriteLine($"║  0. Thoát{' ',53} ║");
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

            Console.Write("Chọn chức năng(0 để thoát) : ");
            string input = Console.ReadLine();

            if (int.TryParse(input, out int choice) && choice >= 0 && choice <= options.Count)
            {
                return choice;
            }

            DisplayError("Lựa chọn không hợp lệ!");
            return -1;
        }

        public static bool Confirm(string message)
        {
            Console.Write($"\n{message} (y/n): ");
            string input = Console.ReadLine().ToLower();
            return input == "y" || input == "yes";
        }

        public static void DisplayProgressBar(int progress, int total, int barLength = 50, string completedChar = "█", string remainingChar = "░")
        {
            if (total <= 0) return;

            double percentage = (double)progress / total;
            int bars = (int)(percentage * barLength);

            string progressBar = "[" + new string(completedChar[0], bars) +
                                new string(remainingChar[0], barLength - bars) + "]";

            string percentageText = $"{progress}/{total} ({percentage:P1})";

            // Tạo màu sắc dựa trên phần trăm hoàn thành
            if (percentage < 0.5)
                Console.ForegroundColor = ConsoleColor.Red;
            else if (percentage < 0.8)
                Console.ForegroundColor = ConsoleColor.Yellow;
            else
                Console.ForegroundColor = ConsoleColor.Green;

            Console.Write($"\r{progressBar} {percentageText}");
            Console.ResetColor();

            if (progress == total)
                Console.WriteLine();
        }
    }

    // ==================== MAIN SYSTEM ====================
    public class RestaurantSystem
    {
        private DataRepository repository;
        private UndoRedoService undoRedoService;
        private MemoryManager memoryManager;
        private User currentUser;
        private bool isRunning;
        private const string DATA_FOLDER = "Data";
        private const string DOWNLOAD_FOLDER = "Downloads";
        private const string BACKUP_FOLDER = "Backup";

        // Danh sách nhóm món cố định
        private List<string> dishCategories = new List<string>
        {
            "Món khai vị", "Món chính", "Món phụ", "Tráng miệng", "Đồ uống",
            "Lẩu", "Nướng", "Xào", "Hấp", "Chiên", "Khai vị lạnh", "Salad",
            "Súp", "Món chay", "Hải sản", "Thịt", "Gà", "Bò", "Heo", "Món đặc biệt"
        };

        public RestaurantSystem()
        {
            repository = new DataRepository();
            undoRedoService = new UndoRedoService();
            memoryManager = new MemoryManager(this);
            currentUser = null;
            isRunning = true;

            // Đăng ký events
            undoRedoService.OnCommandExecuted += (desc) =>
                Logger.Info($"Command executed: {desc}", "UndoRedo");
            undoRedoService.OnCommandUndone += (desc) =>
                Logger.Info($"Command undone: {desc}", "UndoRedo");
            undoRedoService.OnCommandRedone += (desc) =>
                Logger.Info($"Command redone: {desc}", "UndoRedo");

            EnsureDirectories();
            LoadAllData();

            Logger.Info("RestaurantSystem initialized", "System");
        }

        public DataRepository GetRepository() { return repository; }

        public void Run()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.Title = "HỆ THỐNG QUẢN LÝ NHÀ HÀNG - RESTAURANT MANAGEMENT SYSTEM";

            // Chạy unit tests
            RunUnitTests();

            DisplayWelcomeScreen();

            while (isRunning)
            {
                if (currentUser == null)
                {
                    ShowLoginScreen();
                }
                else
                {
                    ShowMainMenu();
                }
            }

            SaveAllData();
            Logger.Info("System shutdown", "System");
            EnhancedUI.DisplaySuccess("Cảm ơn bạn đã sử dụng hệ thống!");
        }

        private void RunUnitTests()
        {
            if (!EnhancedUI.Confirm("Chạy unit tests trước khi khởi động?"))
            {
                EnhancedUI.DisplayHeader("⏭️  KHỞI ĐỘNG NHANH");
                Console.WriteLine("Bỏ qua kiểm tra chất lượng...");
                EnhancedUI.DisplayProgressBar(1, 1, 40);
                Console.WriteLine("🚀 Khởi động hệ thống trực tiếp!");
                Console.WriteLine("\n════════════════════════════════════════════════════════════════");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("👉 NHẤN PHÍM BẤT KỲ ĐỂ TIẾP TỤC...");
                Console.ResetColor();
                Console.ReadKey();
                Thread.Sleep(500);
                return;
            }

            string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(downloadsPath))
                Directory.CreateDirectory(downloadsPath);

            string logFile = Path.Combine(downloadsPath, $"TestReport_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            var logBuilder = new StringBuilder();
            logBuilder.AppendLine("===== 🧩 BÁO CÁO UNIT TEST CHI TIẾT (TRỌNG SỐ) =====");
            logBuilder.AppendLine($"🕒 Thời gian bắt đầu: {DateTime.Now}");
            logBuilder.AppendLine("════════════════════════════════════════════════════════════════");

            try
            {
                EnhancedUI.DisplayHeader("🚀 HỆ THỐNG KIỂM TRA CHẤT LƯỢNG");

                // === Cấu hình module test với trọng số ===
                var testModules = new List<(string Name, Action TestAction, double Weight)>
        {
            ("🔐 Bảo mật hệ thống", UnitTests.TestSecurityService, 2.0),
            ("🔄 Command Pattern", UnitTests.TestCommandPattern, 1.5),
            ("💾 Quản lý bộ nhớ", UnitTests.TestMemoryManagement, 1.5),
            ("📊 Business Logic", () => { try { UnitTests.TestBusinessLogic(); } catch { UnitTests.TestBusinessLogicWithCleanEnvironment(); } }, 3.0),
            ("🔗 Kiểm thử tích hợp", UnitTests.TestIntegration, 2.0)
        };

                double totalWeight = testModules.Sum(t => t.Weight);
                double earnedWeight = 0.0;
                var results = new List<(string Name, bool Passed, double Time, string Note, double Weight)>();
                double totalTime = 0.0;

                for (int i = 0; i < testModules.Count; i++)
                {
                    var module = testModules[i];
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n════════════════════════════════════════════════════════════════");
                    Console.WriteLine($"🧩 Đang kiểm tra ({i + 1}/{testModules.Count}): {module.Name}");
                    Console.ResetColor();

                    // Progress bar
                    for (int j = 0; j <= 50; j++)
                    {
                        Console.Write($"\r[{new string('█', j)}{new string(' ', 50 - j)}] {j * 2}%");
                        Thread.Sleep(15);
                    }
                    Console.WriteLine();

                    DateTime start = DateTime.Now;
                    try
                    {
                        module.TestAction.Invoke();
                        double time = (DateTime.Now - start).TotalSeconds;
                        totalTime += time;
                        earnedWeight += module.Weight;

                        results.Add((module.Name, true, time, "Không có lỗi", module.Weight));
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✅ {module.Name} - PASSED ({time:F2}s) | Trọng số: {module.Weight}");
                        logBuilder.AppendLine($"✅ {module.Name} - PASSED ({time:F2}s) | Trọng số: {module.Weight}");
                    }
                    catch (Exception ex)
                    {
                        double time = (DateTime.Now - start).TotalSeconds;
                        totalTime += time;

                        results.Add((module.Name, false, time, ex.Message, module.Weight));
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"❌ {module.Name} - FAILED ({time:F2}s) | Trọng số: {module.Weight}");
                        logBuilder.AppendLine($"❌ {module.Name} - FAILED ({time:F2}s) | Trọng số: {module.Weight}");
                        logBuilder.AppendLine($"   🔎 Chi tiết lỗi: {ex.Message}");
                    }
                    Console.ResetColor();
                    Thread.Sleep(400);
                }

                // === Bảng tổng kết chi tiết với trọng số ===
                Console.WriteLine("\n════════════════════════════════════════════════════════════════");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("📊 BẢNG TỔNG HỢP KIỂM TRA CHI TIẾT (TRỌNG SỐ)");
                Console.ResetColor();
                Console.WriteLine("════════════════════════════════════════════════════════════════");

                string header = $"| {"STT",-3} | {"TÊN MODULE",-25} | {"TRẠNG THÁI",-10} | {"THỜI GIAN (s)",-12} | {"GHI CHÚ",-35} | {"TRỌNG SỐ",-8} |";
                Console.WriteLine(header);
                Console.WriteLine(new string('─', header.Length));

                int index = 1;
                foreach (var r in results)
                {
                    Console.ForegroundColor = r.Passed ? ConsoleColor.Green : ConsoleColor.Red;
                    Console.WriteLine($"| {index,-3} | {r.Name,-25} | {(r.Passed ? "PASSED" : "FAILED"),-10} | {r.Time,12:F2} | {r.Note,-35} | {r.Weight,8:F1} |");
                    Console.ResetColor();
                    index++;
                }
                Console.WriteLine(new string('─', header.Length));

                double systemScore = Math.Round(earnedWeight / totalWeight * 100, 2);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"⏱ Tổng thời gian chạy: {totalTime:F2}s");
                Console.WriteLine($"📈 Điểm chất lượng hệ thống: {systemScore}%");
                Console.ResetColor();

                // Ghi log chi tiết
                logBuilder.AppendLine("\n════════════════════════════════════════════════════════════════");
                logBuilder.AppendLine($"Tổng thời gian chạy: {totalTime:F2}s");
                logBuilder.AppendLine($"Điểm chất lượng hệ thống: {systemScore}%");
                File.WriteAllText(logFile, logBuilder.ToString(), Encoding.UTF8);

                Console.WriteLine("\n📁 Báo cáo chi tiết đã lưu tại:");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(logFile);
                Console.ResetColor();

                Console.WriteLine("\n👉 NHẤN PHÍM BẤT KỲ ĐỂ TIẾP TỤC KHỞI ĐỘNG HỆ THỐNG...");
                Console.ReadKey(true);
            }
            catch (Exception ex)
            {
                EnhancedUI.DisplayError($"💥 Lỗi trong quá trình chạy tests: {ex.Message}");
                logBuilder.AppendLine($"\n💥 LỖI HỆ THỐNG: {ex.Message}");
                File.WriteAllText(logFile, logBuilder.ToString(), Encoding.UTF8);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n👉 NHẤN PHÍM BẤT KỲ ĐỂ THOÁT...");
                Console.ResetColor();
                Console.ReadKey();
                Environment.Exit(1);
            }
        }


        // ==================== ENHANCED DIRECTORY MANAGEMENT ====================
        private void EnsureDirectories()
        {
            try
            {
                // Tạo tất cả các thư mục cần thiết
                string[] directories = {
            DATA_FOLDER,
            BACKUP_FOLDER,
            Path.Combine(DATA_FOLDER, "Archives"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER)
        };

                foreach (string directory in directories)
                {
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                        Logger.Info($"Created directory: {directory}", "System");
                    }
                }

                // Kiểm tra quyền ghi
                CheckWritePermissions();

                Logger.Info("All directories ensured", "System");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to create directories: {ex.Message}", "System", ex);
                throw;
            }
        }

        private void CheckWritePermissions()
        {
            try
            {
                string testFile = Path.Combine(DATA_FOLDER, "write_test.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                Logger.Info("Write permissions OK", "System");
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Error("No write permission in data directory", "System", ex);
                throw new UnauthorizedAccessException("Không có quyền ghi vào thư mục dữ liệu. Vui lòng chạy chương trình với quyền Administrator hoặc chọn thư mục khác.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Write permission check failed: {ex.Message}", "System", ex);
                throw;
            }
        }

        private void DisplayWelcomeScreen()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;

            // ASCII Art Logo
            Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════════════════════╗
║                                                                              ║
║ ██████╗ ███████╗███████╗████████╗ █████╗ ██╗   ██╗██████╗  █████╗ ███╗   ██╗ ║
║ ██╔══██╗██╔════╝██╔════╝╚══██╔══╝██╔══██╗██║   ██║██╔══██╗██╔══██╗████╗  ██║ ║
║ ██████╔╝█████╗  ███████╗   ██║   ███████║██║   ██║██████╔╝███████║██╔██╗ ██║ ║ 
║ ██╔══██╗██╔══╝  ╚════██║   ██║   ██╔══██║██║   ██║██╔══██╗██╔══██║██║╚██╗██║ ║
║ ██║  ██║███████╗███████║   ██║   ██║  ██║╚██████╔╝██║  ██║██║  ██║██║ ╚████║ ║
║ ╚═╝  ╚═╝╚══════╝╚══════╝   ╚═╝   ╚═╝  ╚═╝ ╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝  ╚═══╝ ║
║                                                                              ║
║                  HỆ THỐNG QUẢN LÝ NHÀ HÀNG CHUYÊN NGHIỆP                     ║
║                                                                              ║
╚══════════════════════════════════════════════════════════════════════════════╝");

            Console.ResetColor();
            Console.WriteLine("\nĐang khởi tạo hệ thống...");

            // Hiển thị progress bar
            for (int i = 0; i <= 100; i += 10)
            {
                EnhancedUI.DisplayProgressBar(i, 100, 30);
                Thread.Sleep(50);
            }

            CheckSystemHealth();
            CheckInventoryWarnings();

            Console.WriteLine("\n\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void CheckSystemHealth()
        {
            Logger.Info("Checking system health", "System");

            try
            {
                // Kiểm tra memory
                string memoryInfo = memoryManager.GetMemoryInfo();
                EnhancedUI.DisplayInfo(memoryInfo);

                // Kiểm tra data integrity
                int dishCount = repository.Dishes.Count;
                int ingredientCount = repository.Ingredients.Count;
                int orderCount = repository.Orders.Count;

                if (dishCount == 0 || ingredientCount == 0)
                {
                    EnhancedUI.DisplayWarning("Hệ thống chưa có dữ liệu. Tạo dữ liệu mẫu...");
                    CreateSampleData();
                }

                // Kiểm tra file permissions
                CheckFilePermissions();

                Logger.Info("System health check completed", "System");
            }
            catch (Exception ex)
            {
                Logger.Error("System health check failed", "System", ex);
                EnhancedUI.DisplayError("Có lỗi trong kiểm tra hệ thống");
            }
        }

        private void CheckFilePermissions()
        {
            try
            {
                string testFile = Path.Combine(DATA_FOLDER, "test_permission.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                Logger.Info("File permissions OK", "System");
            }
            catch (Exception ex)
            {
                Logger.Error("File permissions issue", "System", ex);
                EnhancedUI.DisplayError("Cảnh báo: Vấn đề quyền truy cập file!");
            }
        }

        private void CheckInventoryWarnings()
        {
            var lowStockIngredients = repository.Ingredients.Values.Where(ing => ing.IsLowStock).ToList();
            var outOfStockDishes = repository.Dishes.Values.Where(d => !CheckDishIngredients(d)).ToList();

            if (lowStockIngredients.Any() || outOfStockDishes.Any())
            {
                EnhancedUI.DisplayWarning("⚠️  CẢNH BÁO HỆ THỐNG:");

                if (lowStockIngredients.Any())
                {
                    Console.WriteLine($"- Có {lowStockIngredients.Count} nguyên liệu sắp hết");
                }

                if (outOfStockDishes.Any())
                {
                    Console.WriteLine($"- Có {outOfStockDishes.Count} món không đủ nguyên liệu");
                }
            }
            else
            {
                EnhancedUI.DisplaySuccess("✅ Tất cả nguyên liệu và món ăn đều sẵn sàng");
            }
        }

        private void ShowLoginScreen()
        {
            EnhancedUI.DisplayHeader("ĐĂNG NHẬP HỆ THỐNG");

            int attempts = 0;
            const int maxAttempts = 3;

            while (attempts < maxAttempts)
            {
                Console.Write("Tên đăng nhập: ");
                string username = Console.ReadLine();

                if (username?.ToLower() == "x")
                {
                    isRunning = false;
                    return;
                }

                string password = EnhancedUI.ReadPassword("Mật khẩu: ");

                if (AuthenticateUser(username, password))
                {
                    EnhancedUI.DisplaySuccess($"Đăng nhập thành công! Chào mừng {currentUser.FullName}");

                    repository.AuditLogs.Add(new AuditLog(username, "LOGIN", "SYSTEM", "", "Đăng nhập hệ thống thành công"));
                    SaveAllData();

                    Logger.Info($"User {username} logged in successfully", "Authentication");
                    Thread.Sleep(1500);
                    return;
                }
                else
                {
                    attempts++;
                    int remaining = maxAttempts - attempts;
                    EnhancedUI.DisplayError($"Tên đăng nhập hoặc mật khẩu không đúng! Còn {remaining} lần thử.");

                    Logger.Warning($"Failed login attempt for user {username}", "Authentication");

                    if (remaining == 0)
                    {
                        EnhancedUI.DisplayError("Đã vượt quá số lần thử đăng nhập. Hệ thống sẽ thoát.");
                        Thread.Sleep(3000);
                        isRunning = false;
                        return;
                    }
                }
            }
        }

        private bool AuthenticateUser(string username, string password)
        {
            if (repository.Users.ContainsKey(username))
            {
                var user = repository.Users[username];
                if (user.IsActive && SecurityService.VerifyPassword(password, user.PasswordHash))
                {
                    currentUser = user;
                    return true;
                }
            }
            return false;
        }

        private void ShowMainMenu()
        {
            var menuOptions = new List<string>
            {
                "Quản lý món ăn",
                "Quản lý nguyên liệu & tồn kho",
                "Quản lý combo & khuyến mãi",
                "Bán hàng / đơn đặt món",
                "Thống kê & báo cáo",
                "Quản lý người dùng",
                "Tiện ích & cảnh báo",
                "Đổi mật khẩu",
                "Undo/Redo & Lịch sử",
                "Hệ thống & Cài đặt"
            };

            // Ẩn quản lý user nếu không phải admin/manager
            if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Manager)
            {
                menuOptions.RemoveAt(5); // Remove "Quản lý người dùng"
            }

            while (true)
            {
                int choice = EnhancedUI.ShowMenu("MENU CHÍNH", menuOptions);

                if (choice == 0)
                {
                    currentUser = null;
                    return;
                }

                ProcessMainMenuChoice(choice);
            }
        }

        private void ProcessMainMenuChoice(int choice)
        {
            // Điều chỉnh choice nếu menu bị ẩn
            if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Manager && choice >= 6)
            {
                choice++;
            }

            switch (choice)
            {
                case 1: ShowDishManagementMenu(); break;
                case 2: ShowIngredientManagementMenu(); break;
                case 3: ShowComboManagementMenu(); break;
                case 4: ShowOrderManagementMenu(); break;
                case 5: ShowReportMenu(); break;
                case 6: ShowUserManagementMenu(); break;
                case 7: ShowUtilityMenu(); break;
                case 8: ChangePassword(); break;
                case 9: ShowUndoRedoMenu(); break;
                case 10: ShowSystemSettingsMenu(); break;
                default:
                    EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                    Thread.Sleep(1000);
                    break;
            }
        }

        // ==================== ENHANCED DISH MANAGEMENT ====================
        private void ShowDishManagementMenu()
        {
            var menuOptions = new List<string>
            {
                "Xem danh sách món ăn",
                "Thêm món ăn mới",
                "Thêm món ăn từ file",
                "Cập nhật món ăn",
                "Xóa món ăn",
                "Tìm kiếm món ăn",
                "Lọc món ăn",
                "Xem chi tiết món ăn",
                "Quản lý nguyên liệu cho món",
                "Cập nhật hàng loạt",
                "Tính toán chi phí & lợi nhuận"
            };

            while (true)
            {
                int choice = EnhancedUI.ShowMenu("QUẢN LÝ MÓN ĂN", menuOptions);
                if (choice == 0) return;

                switch (choice)
                {
                    case 1: DisplayDishes(); break;
                    case 2: AddDish(); break;
                    case 3: AddDishesFromFile(); break;
                    case 4: UpdateDish(); break;
                    case 5: DeleteDish(); break;
                    case 6: SearchDishes(); break;
                    case 7: FilterDishes(); break;
                    case 8: ShowDishDetail(); break;
                    case 9:
                        {
                            int currentPage = 1;
                            const int pageSize = 10;
                            bool exitIngredientMenu = false;

                            while (!exitIngredientMenu)
                            {
                                Console.Clear();
                                EnhancedUI.DisplayHeader("🥦 QUẢN LÝ NGUYÊN LIỆU CHO MÓN ĂN");

                                var dishList = repository.Dishes.Values.OrderBy(d => d.Id).ToList();
                                if (!dishList.Any())
                                {
                                    Console.WriteLine("\n⚠️  Chưa có món ăn nào trong hệ thống.");
                                    Console.WriteLine("Nhấn phím bất kỳ để quay lại...");
                                    Console.ReadKey();
                                    break;
                                }

                                int totalPages = Math.Max(1, (int)Math.Ceiling((double)dishList.Count / pageSize));
                                if (currentPage > totalPages) currentPage = totalPages;

                                var pageItems = dishList
                                    .Skip((currentPage - 1) * pageSize)
                                    .Take(pageSize)
                                    .ToList();

                                Console.WriteLine("╔════════════╦══════════════════════════════════════╦══════════════╦══════════════╗");
                                Console.WriteLine("║   MÃ MÓN   ║ TÊN MÓN                              ║    GIÁ (VNĐ) ║  TRẠNG THÁI  ║");
                                Console.WriteLine("╠════════════╬══════════════════════════════════════╬══════════════╬══════════════╣");

                                foreach (var item in pageItems)
                                {
                                    Console.WriteLine("║ {0,-10} ║ {1,-36} ║ {2,12:N0} ║ {3,-12}║",
                                        item.Id,
                                        TruncateString(item.Name, 36),
                                        item.Price,
                                        item.IsAvailable ? "✅ Có sẵn" : "❌ Hết hàng");
                                }

                                Console.WriteLine("╚════════════╩══════════════════════════════════════╩══════════════╩══════════════╝");
                                Console.WriteLine($"Trang {currentPage}/{totalPages} | Tổng: {dishList.Count} món");
                                Console.WriteLine("──────────────────────────────────────────────────────────────────────────────");
                                Console.WriteLine("Nhập số trang (VD: 2), hoặc nhập MÃ MÓN để quản lý nguyên liệu.");
                                Console.WriteLine("Nhập 'auto' để tự động gán nguyên liệu cho tất cả món.");
                                Console.WriteLine("Nhập '0' để quay lại menu chính.");
                                Console.Write("\n👉 Lựa chọn: ");

                                string input = Console.ReadLine()?.Trim();
                                if (string.IsNullOrEmpty(input)) continue;

                                if (string.Equals(input, "0", StringComparison.OrdinalIgnoreCase))
                                {
                                    exitIngredientMenu = true;
                                    break;
                                }

                                if (int.TryParse(input, out int pageNum))
                                {
                                    if (pageNum >= 1 && pageNum <= totalPages)
                                    {
                                        currentPage = pageNum;
                                        Thread.Sleep(100);
                                        continue;
                                    }
                                    else
                                    {
                                        EnhancedUI.DisplayError("❌ Số trang không hợp lệ!");
                                        Console.ReadKey();
                                        continue;
                                    }
                                }

                                // 🚀 Tự động gán nguyên liệu (tối ưu + tương thích C#7.3)
                                if (string.Equals(input, "auto", StringComparison.OrdinalIgnoreCase))
                                {
                                    Console.Clear();
                                    EnhancedUI.DisplayHeader("🤖 TỰ ĐỘNG GÁN NGUYÊN LIỆU CHO TOÀN BỘ MÓN");

                                    int totalDishes = repository.Dishes.Count;
                                    int totalAssigned = 0;
                                    List<string> missingIngredientsReport = new List<string>();

                                    Console.WriteLine("Bắt đầu gán nguyên liệu...\n");

                                    int barWidth = 40;
                                    int progress = 0;
                                    int lastPercent = -1;

                                    // Danh sách nguyên liệu dạng lowercase
                                    var ingredients = repository.Ingredients.Values
                                        .Select(i => new { i.Id, NameLower = i.Name.ToLower() })
                                        .ToList();

                                    foreach (var dish in repository.Dishes.Values)
                                    {
                                        string dishNameLower = dish.Name.ToLower();
                                        var matched = ingredients
                                            .Where(i => dishNameLower.Contains(i.NameLower) || i.NameLower.Contains(dishNameLower))
                                            .ToList();

                                        if (matched.Count > 0)
                                        {
                                            if (dish.Ingredients == null)
                                                dish.Ingredients = new Dictionary<string, decimal>();

                                            foreach (var ing in matched)
                                                if (!dish.Ingredients.ContainsKey(ing.Id))
                                                    dish.Ingredients[ing.Id] = 1m;

                                            totalAssigned++;
                                        }
                                        else
                                        {
                                            missingIngredientsReport.Add(dish.Name);
                                        }

                                        // Cập nhật tiến độ mỗi 5%
                                        progress++;
                                        int percent = progress * 100 / totalDishes;
                                        if (percent != lastPercent && percent % 5 == 0)
                                        {
                                            int filled = percent * barWidth / 100;
                                            Console.SetCursorPosition(0, Console.CursorTop);
                                            Console.ForegroundColor = ConsoleColor.Green;
                                            Console.Write(new string('█', filled));
                                            Console.ForegroundColor = ConsoleColor.Gray;
                                            Console.Write(new string('█', barWidth - filled));
                                            Console.ResetColor();
                                            Console.Write($" {percent}% ({progress}/{totalDishes})");
                                            lastPercent = percent;
                                        }
                                    }

                                    Console.WriteLine("\n\n🎯 Hoàn tất tự động gán nguyên liệu!");
                                    EnhancedUI.DisplaySuccess($"{totalAssigned}/{totalDishes} món đã được gán nguyên liệu.");
                                    if (missingIngredientsReport.Count > 0)
                                        Console.WriteLine($"\n⚠️ Có {missingIngredientsReport.Count} món chưa có nguyên liệu phù hợp.");

                                    // Ghi log
                                    repository.AuditLogs.Add(new AuditLog(
                                        currentUser.Username,
                                        "AUTO_ASSIGN_INGREDIENTS",
                                        "SYSTEM",
                                        "",
                                        $"Tự động gán nguyên liệu cho {totalAssigned}/{totalDishes} món"
                                    ));

                                    SaveAllData();

                                    // 📂 Thư mục Downloads
                                    string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

                                    // 📝 Hỏi xuất file
                                    Console.WriteLine("\nBạn có muốn xuất file danh sách món đã gán / chưa gán nguyên liệu?");
                                    Console.WriteLine("1. Xuất món đã gán");
                                    Console.WriteLine("2. Xuất món chưa gán");
                                    Console.WriteLine("3. Xuất cả 2");
                                    Console.WriteLine("0. Không xuất");
                                    Console.Write("👉 Chọn: ");
                                    string choice1 = Console.ReadLine()?.Trim();

                                    // Tạo thư mục nếu chưa có
                                    if (!Directory.Exists(downloadsPath))
                                        Directory.CreateDirectory(downloadsPath);

                                    if (choice1 == "1" || choice1 == "3")
                                    {
                                        string assignedFile = Path.Combine(downloadsPath, "Dishes_Assigned.csv");
                                        File.WriteAllLines(assignedFile, repository.Dishes.Values
                                            .Where(d => d.Ingredients != null && d.Ingredients.Count > 0)
                                            .Select(d => $"{d.Id},{d.Name},{d.Price},{string.Join(";", d.Ingredients.Keys)}"));
                                        Console.WriteLine($"✅ Đã xuất file: {assignedFile}");
                                    }

                                    if (choice1 == "2" || choice1 == "3")
                                    {
                                        // ⚙️ Lọc trùng/giống tên (bỏ món gần giống)
                                        // ⚙️ Lọc trùng hoặc gần giống tên món
                                        var processedNames = new List<string>();
                                        foreach (var name in missingIngredientsReport)
                                        {
                                            // Chuẩn hóa tên: bỏ số, ký tự đặc biệt, chữ thường
                                            string clean = new string(name
                                                .Where(c => char.IsLetter(c) || char.IsWhiteSpace(c))
                                                .ToArray())
                                                .Trim()
                                                .ToLower();

                                            // Kiểm tra có tên tương tự chưa (bắt đầu bằng hoặc chứa nhau)
                                            bool isDuplicate = processedNames.Any(existing =>
                                                existing.StartsWith(clean) || clean.StartsWith(existing) ||
                                                existing.Contains(clean) || clean.Contains(existing));

                                            if (!isDuplicate)
                                                processedNames.Add(clean);
                                        }

                                        // Xuất file món chưa có nguyên liệu
                                        string missingFile = Path.Combine(downloadsPath, "Dishes_Missing.csv");
                                        File.WriteAllLines(missingFile, processedNames.Distinct());
                                        Console.WriteLine($"✅ Đã xuất file: {missingFile}");

                                    }

                                    Console.WriteLine("\nNhấn phím bất kỳ để quay lại menu...");
                                    Console.ReadKey();
                                    continue;
                                }

                                // 🔧 Quản lý nguyên liệu từng món
                                if (repository.Dishes.TryGetValue(input, out var selectedDish))
                                {
                                    Console.Clear();
                                    EnhancedUI.DisplayHeader($"🔧 QUẢN LÝ NGUYÊN LIỆU - {selectedDish.Name}");
                                    ManageDishIngredients(selectedDish);

                                    Console.WriteLine("\nNhấn phím bất kỳ để quay lại danh sách món...");
                                    Console.ReadKey();

                                    totalPages = Math.Max(1, (int)Math.Ceiling((double)repository.Dishes.Count / pageSize));
                                    if (currentPage > totalPages) currentPage = totalPages;
                                }
                                else
                                {
                                    EnhancedUI.DisplayError("⚠️ Mã món không tồn tại!");
                                    Console.ReadKey();
                                }
                            }
                            break;
                        }




                    case 10: BatchUpdateDishes(); break;
                    case 11: CalculateDishCosts(); break;
                }
            }
        }

        private void AddDish()
        {
            ConsoleColor headerColor = ConsoleColor.Cyan;

            while (true)
            {
                Console.Clear();
                Console.ForegroundColor = headerColor;
                Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                   🍽️ THÊM MÓN ĂN MỚI                       ║");
                Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");
                Console.ResetColor();

                Console.WriteLine("🎯 CHẾ ĐỘ NHẬP:");
                Console.WriteLine("1. Nhập từng món (Thủ công)");
                Console.WriteLine("2. Nhập nhiều món cùng lúc (Batch)");
                Console.WriteLine("0. Quay lại");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\n🗳️ Lựa chọn của bạn: ");
                Console.ResetColor();

                string choice = Console.ReadLine();

                if (choice == "0") return;

                if (choice == "1")
                {
                    AddSingleDish();
                }
                else if (choice == "2")
                {
                    AddMultipleDishes();
                }
                else
                {
                    EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                    Console.ReadKey();
                }
            }
        }

        private void AddSingleDish()
        {
            EnhancedUI.DisplayHeader("THÊM MÓN ĂN ĐƠN LẺ");

            try
            {
                Console.Write("Mã món ăn: ");
                string id = Console.ReadLine();

                if (repository.Dishes.ContainsKey(id))
                {
                    EnhancedUI.DisplayError("Mã món ăn đã tồn tại!");
                    return;
                }

                Console.Write("Tên món ăn: ");
                string name = Console.ReadLine();

                Console.Write("Mô tả: ");
                string description = Console.ReadLine();

                Console.Write("Giá bán: ");
                if (!decimal.TryParse(Console.ReadLine(), out decimal price) || price <= 0)
                {
                    EnhancedUI.DisplayError("Giá không hợp lệ!");
                    return;
                }

                // Chọn nhóm món
                string category = SelectCategory();
                if (string.IsNullOrEmpty(category)) return;

                var dish = new Dish(id, name, description, price, category);

                // Thêm nguyên liệu
                if (EnhancedUI.Confirm("Thêm nguyên liệu cho món ăn ngay bây giờ?"))
                {
                    AddIngredientsToDish(dish);
                }

                var command = new AddDishCommand(this, dish);
                undoRedoService.ExecuteCommand(command);

                repository.AuditLogs.Add(new AuditLog(currentUser.Username, "ADD_DISH", "DISH", id, $"Thêm món: {name}"));
                SaveAllData();

                EnhancedUI.DisplaySuccess($"Thêm món ăn '{name}' thành công!");

                // Tính toán chi phí
                dish.CalculateCost(repository.Ingredients);
                EnhancedUI.DisplayInfo($"Chi phí nguyên liệu: {dish.Cost:N0}đ | Lợi nhuận: {dish.ProfitMargin:F1}%");

                Logger.Info($"Dish {id} added successfully", "DishManagement");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to add dish", "DishManagement", ex);
                EnhancedUI.DisplayError($"Lỗi: {ex.Message}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void AddMultipleDishes()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                🍽️ THÊM NHIỀU MÓN ĂN CÙNG LÚC               ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");
            Console.ResetColor();

            Console.WriteLine("📝 HƯỚNG DẪN NHẬP NHIỀU MÓN:");
            Console.WriteLine("• Nhập thông tin mỗi món trên 1 dòng, định dạng:");
            Console.WriteLine("  MÃ_MÓN|TÊN_MÓN|MÔ_TẢ|GIÁ|NHÓM_MÓN");
            Console.WriteLine("• Ví dụ: MON001|Phở bò|Phở bò truyền thống|45000|Món chính");
            Console.WriteLine("• Gõ 'DONE' để kết thúc nhập");
            Console.WriteLine("• Gõ 'CANCEL' để hủy bỏ\n");

            List<Dish> dishesToAdd = new List<Dish>();
            int lineNumber = 1;

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"Món {lineNumber}: ");
                Console.ResetColor();

                string input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input)) continue;

                if (input.ToUpper() == "DONE")
                {
                    if (dishesToAdd.Count == 0)
                    {
                        EnhancedUI.DisplayWarning("Chưa có món nào được nhập!");
                        continue;
                    }
                    break;
                }

                if (input.ToUpper() == "CANCEL")
                {
                    if (EnhancedUI.Confirm("Hủy bỏ toàn bộ quá trình nhập?"))
                    {
                        return;
                    }
                    continue;
                }

                // Parse thông tin món ăn
                string[] parts = input.Split('|');
                if (parts.Length < 5)
                {
                    EnhancedUI.DisplayError("❌ Định dạng không đúng! Cần 5 phần cách nhau bằng |");
                    continue;
                }

                string id = parts[0].Trim();
                string name = parts[1].Trim();
                string description = parts[2].Trim();

                if (!decimal.TryParse(parts[3].Trim(), out decimal price) || price <= 0)
                {
                    EnhancedUI.DisplayError("❌ Giá không hợp lệ!");
                    continue;
                }

                string category = parts[4].Trim();

                // Validate dữ liệu
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
                {
                    EnhancedUI.DisplayError("❌ Mã và tên món không được để trống!");
                    continue;
                }

                if (repository.Dishes.ContainsKey(id))
                {
                    EnhancedUI.DisplayError($"❌ Mã món '{id}' đã tồn tại!");
                    continue;
                }

                // Kiểm tra nhóm món hợp lệ
                if (!IsValidCategory(category))
                {
                    EnhancedUI.DisplayError($"❌ Nhóm món '{category}' không hợp lệ!");
                    Console.WriteLine("📋 Các nhóm món hợp lệ: " + string.Join(", ", GetValidCategories()));
                    continue;
                }

                var dish = new Dish(id, name, description, price, category);
                dishesToAdd.Add(dish);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✅ Đã thêm: {name} - {price:N0}đ");
                Console.ResetColor();

                lineNumber++;
            }

            if (dishesToAdd.Count == 0) return;

            // Hiển thị summary trước khi lưu
            Console.WriteLine($"\n📊 TỔNG KẾT: {dishesToAdd.Count} món sẽ được thêm vào");
            Console.WriteLine("┌────────────┬────────────────────────────┬──────────────┬────────────┐");
            Console.WriteLine("│     ID     │          Tên món          │     Giá      │   Nhóm     │");
            Console.WriteLine("├────────────┼────────────────────────────┼──────────────┼────────────┤");

            foreach (var dish in dishesToAdd)
            {
                Console.WriteLine($"│ {dish.Id,-10} │ {dish.Name,-26} │ {dish.Price,10:N0}đ │ {dish.Category,-10} │");
            }
            Console.WriteLine("└────────────┴────────────────────────────┴──────────────┴────────────┘");

            if (!EnhancedUI.Confirm($"\nXác nhận thêm {dishesToAdd.Count} món vào hệ thống?"))
            {
                return;
            }

            // Thực hiện thêm nhiều món
            Console.WriteLine("\n⏳ Đang thêm món vào hệ thống...\n");

            int successCount = 0;
            int failedCount = 0;

            for (int i = 0; i < dishesToAdd.Count; i++)
            {
                var dish = dishesToAdd[i];

                Console.Write($"⏳ Đang xử lý {i + 1}/{dishesToAdd.Count}: {dish.Name}... ");

                try
                {
                    var command = new AddDishCommand(this, dish);
                    undoRedoService.ExecuteCommand(command);

                    repository.AuditLogs.Add(new AuditLog(currentUser.Username, "ADD_DISH_BATCH", "DISH", dish.Id, $"Thêm món batch: {dish.Name}"));
                    successCount++;

                    // Tính toán chi phí
                    dish.CalculateCost(repository.Ingredients);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ Thành công | Chi phí: {dish.Cost:N0}đ | Lợi nhuận: {dish.ProfitMargin:F1}%");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    failedCount++;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ Thất bại: {ex.Message}");
                    Console.ResetColor();
                    Logger.Error($"Failed to add dish {dish.Id} in batch", "DishManagement", ex);
                }
            }

            // Lưu toàn bộ dữ liệu
            SaveAllData();

            // Hiển thị kết quả cuối cùng
            Console.WriteLine("\n" + new string('═', 60));
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("🎊 KẾT QUẢ THÊM MÓN:");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ Thành công: {successCount} món");
            Console.ResetColor();

            if (failedCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Thất bại: {failedCount} món");
                Console.ResetColor();
            }

            Console.WriteLine($"📊 Tổng số món trong hệ thống: {repository.Dishes.Count}");

            Logger.Info($"Batch add dishes completed: {successCount} success, {failedCount} failed", "DishManagement");

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\n⏎ Nhấn phím bất kỳ để tiếp tục...");
            Console.ResetColor();
            Console.ReadKey();
        }

        private bool IsValidCategory(string category)
        {
            // Giả sử bạn có danh sách các nhóm món hợp lệ
            var validCategories = GetValidCategories();
            return validCategories.Contains(category);
        }

        private List<string> GetValidCategories()
        {
            // Trả về danh sách các nhóm món hợp lệ từ repository hoặc hardcode
            return new List<string> { "Món chính", "Món khai vị", "Món tráng miệng", "Đồ uống", "Món đặc biệt" };
        }

        private string SelectCategory()
        {
            EnhancedUI.DisplayInfo("Chọn nhóm món ăn:");
            for (int i = 0; i < dishCategories.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {dishCategories[i]}");
            }
            Console.WriteLine($"0. Nhập nhóm mới");

            Console.Write("Chọn: ");
            string choice = Console.ReadLine();

            if (int.TryParse(choice, out int index) && index > 0 && index <= dishCategories.Count)
            {
                return dishCategories[index - 1];
            }
            else if (index == 0)
            {
                Console.Write("Nhập tên nhóm mới: ");
                return Console.ReadLine();
            }

            return "Món chính";
        }

        private void AddIngredientsToDish(Dish dish)
        {
            EnhancedUI.DisplayInfo("THÊM NGUYÊN LIỆU CHO MÓN ĂN");

            while (true)
            {
                DisplayIngredientsForSelection(1, 10);

                Console.Write("Mã nguyên liệu (để trống để kết thúc): ");
                string ingId = Console.ReadLine();

                if (string.IsNullOrEmpty(ingId)) break;

                if (!repository.Ingredients.ContainsKey(ingId))
                {
                    EnhancedUI.DisplayError("Nguyên liệu không tồn tại!");
                    continue;
                }

                Console.Write($"Số lượng ({repository.Ingredients[ingId].Unit}): ");
                if (!decimal.TryParse(Console.ReadLine(), out decimal quantity) || quantity <= 0)
                {
                    EnhancedUI.DisplayError("Số lượng không hợp lệ!");
                    continue;
                }

                dish.Ingredients[ingId] = quantity;
                EnhancedUI.DisplaySuccess($"Đã thêm {repository.Ingredients[ingId].Name} vào món ăn!");

                if (!EnhancedUI.Confirm("Tiếp tục thêm nguyên liệu?")) break;
            }
        }

        private void DisplayIngredientsForSelection(int page = 1, int pageSize = 10)
        {
            var ingredientList = repository.Ingredients.Values.ToList();
            int totalPages = (int)Math.Ceiling(ingredientList.Count / (double)pageSize);

            Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                     CHỌN NGUYÊN LIỆU                         ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-8} {1,-25} {2,-10} {3,-12} ║",
                "Mã", "Tên", "Đơn vị", "Giá/ĐV");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

            var pagedIngredients = ingredientList.Skip((page - 1) * pageSize).Take(pageSize);
            foreach (var ing in pagedIngredients)
            {
                Console.WriteLine("║ {0,-8} {1,-25} {2,-10} {3,-12} ║",
                    ing.Id,
                    ing.Name.TruncateString(25),
                    ing.Unit,
                    $"{ing.PricePerUnit:N0}đ");
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

            Console.WriteLine($"Trang {page}/{totalPages}");
        }

        private void ManageDishIngredients(Dish dish)
        {
            EnhancedUI.DisplayHeader($"QUẢN LÝ NGUYÊN LIỆU CHO MÓN: {dish.Name}");

            while (true)
            {
                Console.WriteLine("Nguyên liệu hiện tại:");
                if (dish.Ingredients.Any())
                {
                    foreach (var ing in dish.Ingredients)
                    {
                        if (repository.Ingredients.TryGetValue(ing.Key, out var ingredient))
                        {
                            Console.WriteLine($"- {ingredient.Name}: {ing.Value} {ingredient.Unit}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Chưa có nguyên liệu");
                }

                Console.WriteLine("\n1. Thêm nguyên liệu");
                Console.WriteLine("2. Xóa nguyên liệu");
                Console.WriteLine("3. Cập nhật số lượng");
                Console.WriteLine("0. Quay lại");
                Console.Write("Chọn: ");

                string choice = Console.ReadLine();
                switch (choice)
                {
                    case "1":
                        AddIngredientsToDish(dish);
                        break;
                    case "2":
                        RemoveIngredientFromDish(dish);
                        break;
                    case "3":
                        UpdateIngredientQuantity(dish);
                        break;
                    case "0":
                        return;
                    default:
                        EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                        break;
                }

                repository.AuditLogs.Add(new AuditLog(
                    currentUser.Username,
                    "UPDATE_DISH_INGREDIENTS",
                    "DISH",
                    dish.Id,
                    $"Cập nhật nguyên liệu cho món {dish.Name}"
                ));
                SaveAllData();
            }
        }

        private void RemoveIngredientFromDish(Dish dish)
        {
            if (!dish.Ingredients.Any())
            {
                EnhancedUI.DisplayWarning("Món ăn chưa có nguyên liệu!");
                return;
            }

            Console.Write("Mã nguyên liệu cần xóa: ");
            string ingId = Console.ReadLine();

            if (dish.Ingredients.ContainsKey(ingId))
            {
                dish.Ingredients.Remove(ingId);
                EnhancedUI.DisplaySuccess("Đã xóa nguyên liệu!");
            }
            else
            {
                EnhancedUI.DisplayError("Nguyên liệu không tồn tại trong món ăn!");
            }
        }

        private void UpdateIngredientQuantity(Dish dish)
        {
            if (!dish.Ingredients.Any())
            {
                EnhancedUI.DisplayWarning("Món ăn chưa có nguyên liệu!");
                return;
            }

            Console.Write("Mã nguyên liệu: ");
            string ingId = Console.ReadLine();

            if (!dish.Ingredients.ContainsKey(ingId))
            {
                EnhancedUI.DisplayError("Nguyên liệu không tồn tại trong món ăn!");
                return;
            }

            var ingredient = repository.Ingredients[ingId];
            Console.Write($"Số lượng mới ({ingredient.Unit}): ");
            if (decimal.TryParse(Console.ReadLine(), out decimal quantity) && quantity > 0)
            {
                dish.Ingredients[ingId] = quantity;
                EnhancedUI.DisplaySuccess("Đã cập nhật số lượng!");
            }
            else
            {
                EnhancedUI.DisplayError("Số lượng không hợp lệ!");
            }
        }

       private void CalculateDishCosts()
{
    EnhancedUI.DisplayHeader("📊 PHÂN TÍCH CHI PHÍ & LỢI NHUẬN MÓN ĂN");

    if (repository.Dishes.Count == 0)
    {
        EnhancedUI.DisplayWarning("Không có món ăn nào trong hệ thống!");
        Console.ReadKey();
        return;
    }

    // Tính chi phí cho tất cả món (đảm bảo cập nhật dish.Cost & dish.ProfitMargin nếu có)
    foreach (var d in repository.Dishes.Values)
    {
        try
        {
            d.CalculateCost(repository.Ingredients);
        }
        catch
        {
            // Bỏ qua nếu có lỗi tính chi phí cho món (vẫn sẽ báo là chưa có NL)
        }
    }

    // Lọc: chỉ hiển thị các món đã được gán nguyên liệu (có ít nhất 1 nguyên liệu)
    var dishesWithIngredients = repository.Dishes.Values
        .Where(x => x.Ingredients != null && x.Ingredients.Any())
        .ToList();

    var dishesWithoutIngredients = repository.Dishes.Values
        .Where(x => x.Ingredients == null || !x.Ingredients.Any())
        .ToList();

    int updatedCount = dishesWithIngredients.Count;
    int noIngredientCount = dishesWithoutIngredients.Count;

    // Hiển thị bảng chỉ các món đã có nguyên liệu
    Console.WriteLine();
    Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║ {0,-10} | {1,-30} | {2,12} | {3,12} | {4,8} ║", "Mã", "Tên Món", "Giá Bán", "Chi Phí", "LN (%)");
    Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════════╣");

    foreach (var dish in dishesWithIngredients)
    {
        // Hiển thị an toàn: nếu Cost = 0 thì hiển "N/A"
        string costDisplay = dish.Cost > 0m ? dish.Cost.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) : "N/A";
        string marginDisplay = dish.Cost > 0m ? $"{dish.ProfitMargin:F1}%" : "N/A";

        Console.WriteLine("║ {0,-10} | {1,-30} | {2,12:N0} | {3,12} | {4,7} ║",
            dish.Id,
            TruncateString(dish.Name, 30),
            dish.Price,
            costDisplay,
            marginDisplay);
    }

    Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════════╝");

    EnhancedUI.DisplaySuccess($"✅ Hiển thị {updatedCount} món đã có nguyên liệu.");
    if (noIngredientCount > 0)
        EnhancedUI.DisplayWarning($"⚠️ Có {noIngredientCount} món chưa được gán nguyên liệu.");

    // Thống kê tổng
    decimal totalCost = dishesWithIngredients.Sum(d => d.Cost);
    decimal totalProfit = dishesWithIngredients.Sum(d => (d.Price - d.Cost));
    decimal avgProfitMargin = dishesWithIngredients.Any() ? dishesWithIngredients.Average(d => d.Cost > 0 ? d.ProfitMargin : 0m) : 0m;

    Console.WriteLine($"\n💰 Tổng chi phí (các món có NL): {totalCost:N0}đ");
    Console.WriteLine($"📈 Tổng lợi nhuận (các món có NL): {totalProfit:N0}đ");
    Console.WriteLine($"📊 Tỷ suất lợi nhuận trung bình: {avgProfitMargin:F1}%");

    // Top lists (chỉ tính những món có Cost > 0)
    var withValidCost = dishesWithIngredients.Where(d => d.Cost > 0m).ToList();
    var topHigh = withValidCost.OrderByDescending(d => d.ProfitMargin).Take(5).ToList();
    var topLow = withValidCost.OrderBy(d => d.ProfitMargin).Take(5).ToList();

    Console.WriteLine("\n🏆 TOP 5 MÓN LỢI NHUẬN CAO:");
    if (topHigh.Any())
    {
        foreach (var t in topHigh)
            Console.WriteLine($"- {t.Name}: {t.ProfitMargin:F1}% (Giá: {t.Price:N0}đ, CP: {t.Cost:N0}đ)");
    }
    else Console.WriteLine("- Không có món đủ dữ liệu.");

    Console.WriteLine("\n⚠️ TOP 5 MÓN LỢI NHUẬN THẤP:");
    if (topLow.Any())
    {
        foreach (var b in topLow)
        {
            string suggestion = b.ProfitMargin < 10 ? "👉 Nên tăng giá/ tối ưu NL" : "✔️ Ổn định";
            Console.WriteLine($"- {b.Name}: {b.ProfitMargin:F1}% (Giá: {b.Price:N0}đ, CP: {b.Cost:N0}đ) {suggestion}");
        }
    }
    else Console.WriteLine("- Không có món đủ dữ liệu.");

    // Gợi ý xuất file
    Console.WriteLine("\nBạn có muốn xuất file không? Chọn:");
    Console.WriteLine("1 - Xuất file CHI TIẾT các món (Id,Name,Description,Price,Category)");
    Console.WriteLine("2 - Xuất file TỔNG HỢP (summary + top lists)");
    Console.WriteLine("3 - Xuất cả 2");
    Console.WriteLine("0 - Không xuất");
    Console.Write("\nLựa chọn của bạn: ");
    string opt = Console.ReadLine()?.Trim();

    int option;
    if (!int.TryParse(opt, out option)) option = 0;

    // helper: chuẩn bị thư mục Downloads
    string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    try
    {
        if (!Directory.Exists(downloadsPath))
            Directory.CreateDirectory(downloadsPath);
    }
    catch
    {
        // fallback: dùng current directory
        downloadsPath = Environment.CurrentDirectory;
    }

    // helper để escape CSV field an toàn
    Func<string, string> EscapeCsv = (s) =>
    {
        if (s == null) return "\"\"";
        string v = s.Replace("\"", "\"\"");
        return $"\"{v}\"";
    };

    // Hàm ghi file chi tiết (danh sách các món: Id,Name,Description,Price,Category)
    Action<string, IEnumerable<Dish>> WriteDetailCsv = (filePath, list) =>
    {
        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var bw = new StreamWriter(fs, new System.Text.UTF8Encoding(true))) // UTF8 BOM
        {
            bw.WriteLine("Id,Name,Description,Price,Category");
            foreach (var d in list)
            {
                // ghi số theo InvariantCulture để tránh dấu phân nghìn
                string price = d.Price.ToString(System.Globalization.CultureInfo.InvariantCulture);
                bw.WriteLine(string.Join(",", new string[]
                {
                    EscapeCsv(d.Id),
                    EscapeCsv(d.Name),
                    EscapeCsv(d.Description),
                    EscapeCsv(price),
                    EscapeCsv(d.Category)
                }));
            }
        }
    };

    // Hàm ghi file summary (tổng hợp)
    Action<string> WriteSummaryCsv = (filePath) =>
    {
        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var bw = new StreamWriter(fs, new System.Text.UTF8Encoding(true)))
        {
            bw.WriteLine("Metric,Value");
            bw.WriteLine($"TotalDishes,{repository.Dishes.Count}");
            bw.WriteLine($"DishesWithIngredients,{dishesWithIngredients.Count}");
            bw.WriteLine($"DishesWithoutIngredients,{dishesWithoutIngredients.Count}");
            bw.WriteLine($"TotalCost,{totalCost.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            bw.WriteLine($"TotalProfit,{totalProfit.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            bw.WriteLine($"AvgProfitMargin,{avgProfitMargin.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            bw.WriteLine();

            bw.WriteLine("TOP5_HighProfit,ProfitMargin");
            foreach (var t in topHigh)
                bw.WriteLine($"{EscapeCsv(t.Name)},{t.ProfitMargin.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

            bw.WriteLine();
            bw.WriteLine("TOP5_LowProfit,ProfitMargin");
            foreach (var b in topLow)
                bw.WriteLine($"{EscapeCsv(b.Name)},{b.ProfitMargin.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }
    };

    // File danh sách món chưa có nguyên liệu (luôn xuất nếu user chọn xuất bất kỳ file nào)
    Action<string> WriteNoIngredientCsv = (filePath) =>
    {
        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var bw = new StreamWriter(fs, new System.Text.UTF8Encoding(true)))
        {
            bw.WriteLine("Id,Name,Description,Price,Category");
            foreach (var d in dishesWithoutIngredients)
            {
                string price = d.Price.ToString(System.Globalization.CultureInfo.InvariantCulture);
                bw.WriteLine(string.Join(",", new string[]
                {
                    EscapeCsv(d.Id),
                    EscapeCsv(d.Name),
                    EscapeCsv(d.Description),
                    EscapeCsv(price),
                    EscapeCsv(d.Category)
                }));
            }
        }
    };

    // Thực thi xuất theo lựa chọn
    try
    {
        if (option == 1 || option == 3)
        {
            string detailPath = Path.Combine(downloadsPath, $"DishDetails_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            WriteDetailCsv(detailPath, dishesWithIngredients);
            EnhancedUI.DisplaySuccess($"✅ Đã xuất file chi tiết các món có nguyên liệu: {detailPath}");

            // xuất file danh sách chưa có nguyên liệu
            if (dishesWithoutIngredients.Any())
            {
                string noIngPath = Path.Combine(downloadsPath, $"Dishes_NoIngredients_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                WriteNoIngredientCsv(noIngPath);
                EnhancedUI.DisplaySuccess($"✅ Đã xuất file các món CHƯA có nguyên liệu: {noIngPath}");
            }
        }

        if (option == 2 || option == 3)
        {
            string summaryPath = Path.Combine(downloadsPath, $"DishSummary_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            WriteSummaryCsv(summaryPath);
            EnhancedUI.DisplaySuccess($"✅ Đã xuất file tổng hợp: {summaryPath}");
        }

        if (option == 0)
        {
            Console.WriteLine("Không xuất file.");
        }
    }
    catch (Exception ex)
    {
        EnhancedUI.DisplayError("❌ Lỗi khi xuất file: " + ex.Message);
    }

    // Ghi log & save
    repository.AuditLogs.Add(new AuditLog(
        currentUser.Username,
        "CALCULATE_COSTS_EXPORT",
        "SYSTEM",
        "",
        $"Tính toán chi phí (hiển thị {dishesWithIngredients.Count}, không có NL {dishesWithoutIngredients.Count}), xuất option: {option}"
    ));
    SaveAllData();

    Console.WriteLine("\nNhấn phím bất kỳ để quay lại menu...");
    Console.ReadKey();
}



        // ==================== ENHANCED INGREDIENT MANAGEMENT ====================
        private void ShowIngredientManagementMenu()
        {
            var menuOptions = new List<string>
            {
                "Xem danh sách nguyên liệu",
                "Thêm nguyên liệu mới",
                "Thêm nguyên liệu từ file",
                "Cập nhật nguyên liệu",
                "Xóa nguyên liệu",
                "Nhập/xuất kho",
                "Xem cảnh báo tồn kho",
                "Cập nhật hàng loạt",
                "Thống kê nguyên liệu",
                "Đặt hàng nguyên liệu"
            };

            while (true)
            {
                int choice = EnhancedUI.ShowMenu("QUẢN LÝ NGUYÊN LIỆU", menuOptions);
                if (choice == 0) return;

                switch (choice)
                {
                    case 1: DisplayIngredients(); break;
                    case 2: AddIngredient(); break;
                    case 3: AddIngredientsFromFile(); break;
                    case 4: UpdateIngredient(); break;
                    case 5: DeleteIngredient(); break;
                    case 6: ShowInventoryMenu(); break;
                    case 7: ShowInventoryWarningsDetailed(); break;
                    case 8: BatchUpdateIngredients(); break;
                    case 9: ShowIngredientStatistics(); break;
                    case 10: CreateIngredientOrder(); break;
                }
            }
        }

        private void AddIngredient()
        {
            EnhancedUI.DisplayHeader("THÊM NHIỀU NGUYÊN LIỆU MỚI");

            try
            {
                while (true)
                {
                    Console.Write("\nNhập mã nguyên liệu (hoặc 0 để kết thúc): ");
                    string id = Console.ReadLine()?.Trim();

                    if (id == "0") break;
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        EnhancedUI.DisplayError("❌ Mã nguyên liệu không được để trống!");
                        continue;
                    }

                    if (repository.Ingredients.ContainsKey(id))
                    {
                        EnhancedUI.DisplayError("⚠️ Mã nguyên liệu đã tồn tại!");
                        continue;
                    }

                    Console.Write("Tên nguyên liệu: ");
                    string name = Console.ReadLine()?.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        EnhancedUI.DisplayError("❌ Tên nguyên liệu không được để trống!");
                        continue;
                    }

                    Console.Write("Đơn vị tính: ");
                    string unit = Console.ReadLine()?.Trim();
                    if (string.IsNullOrWhiteSpace(unit))
                    {
                        EnhancedUI.DisplayError("❌ Đơn vị tính không hợp lệ!");
                        continue;
                    }

                    Console.Write("Số lượng tồn kho: ");
                    if (!decimal.TryParse(Console.ReadLine(), out decimal quantity) || quantity < 0)
                    {
                        EnhancedUI.DisplayError("❌ Số lượng không hợp lệ!");
                        continue;
                    }

                    Console.Write("Số lượng tối thiểu: ");
                    if (!decimal.TryParse(Console.ReadLine(), out decimal minQuantity) || minQuantity < 0)
                    {
                        EnhancedUI.DisplayError("❌ Số lượng tối thiểu không hợp lệ!");
                        continue;
                    }

                    Console.Write("Giá mỗi đơn vị: ");
                    if (!decimal.TryParse(Console.ReadLine(), out decimal price) || price < 0)
                    {
                        EnhancedUI.DisplayError("❌ Giá không hợp lệ!");
                        continue;
                    }

                    var ingredient = new Ingredient(id, name, unit, quantity, minQuantity, price);
                    var command = new AddIngredientCommand(this, ingredient);
                    undoRedoService.ExecuteCommand(command);

                    repository.AuditLogs.Add(new AuditLog(currentUser.Username, "ADD_INGREDIENT", "INGREDIENT", id, $"Thêm nguyên liệu: {name}"));

                    EnhancedUI.DisplaySuccess($"✅ Đã thêm: {name} ({quantity} {unit})");
                }

                SaveAllData();
                EnhancedUI.DisplaySuccess("\n🎉 Hoàn tất thêm nguyên liệu hàng loạt!");
                Logger.Info("Batch ingredient addition completed successfully", "IngredientManagement");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to add ingredients batch", "IngredientManagement", ex);
                EnhancedUI.DisplayError($"Lỗi: {ex.Message}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để quay lại menu...");
            Console.ReadKey();
        }


        private void ShowInventoryWarningsDetailed()
        {
            EnhancedUI.DisplayHeader("CẢNH BÁO TỒN KHO CHI TIẾT");

            var lowStockIngredients = repository.Ingredients.Values.Where(ing => ing.IsLowStock).ToList();
            var outOfStockIngredients = repository.Ingredients.Values.Where(ing => ing.Quantity == 0).ToList();

            if (!lowStockIngredients.Any() && !outOfStockIngredients.Any())
            {
                EnhancedUI.DisplaySuccess("✅ Không có cảnh báo tồn kho!");
                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
                return;
            }

            if (outOfStockIngredients.Any())
            {
                EnhancedUI.DisplayError($"🚨 CÓ {outOfStockIngredients.Count} NGUYÊN LIỆU ĐÃ HẾT:");
                foreach (var ing in outOfStockIngredients.Take(10))
                {
                    Console.WriteLine($"- {ing.Name}: 0 {ing.Unit} (Tối thiểu: {ing.MinQuantity} {ing.Unit})");
                }
                Console.WriteLine();
            }

            if (lowStockIngredients.Any())
            {
                EnhancedUI.DisplayWarning($"⚠️  CÓ {lowStockIngredients.Count} NGUYÊN LIỆU SẮP HẾT:");
                foreach (var ing in lowStockIngredients.Take(10))
                {
                    decimal needed = ing.MinQuantity - ing.Quantity;
                    Console.WriteLine($"- {ing.Name}: {ing.Quantity} {ing.Unit} (Cần thêm: {needed} {ing.Unit})");
                }
            }

            // Kiểm tra món ăn bị ảnh hưởng
            var affectedDishes = repository.Dishes.Values.Where(d => !CheckDishIngredients(d)).ToList();
            if (affectedDishes.Any())
            {
                Console.WriteLine($"\n📋 CÓ {affectedDishes.Count} MÓN KHÔNG ĐỦ NGUYÊN LIỆU:");
                foreach (var dish in affectedDishes.Take(5))
                {
                    Console.WriteLine($"- {dish.Name}");
                }
            }

            // Xuất báo cáo
            if (EnhancedUI.Confirm("\nXuất báo cáo cảnh báo ra file?"))
            {
                ExportInventoryWarningReport(lowStockIngredients, outOfStockIngredients, affectedDishes);
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ExportInventoryWarningReport(List<Ingredient> lowStock, List<Ingredient> outOfStock, List<Dish> affectedDishes)
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"BaoCaoCanhBaoTonKho_{DateTime.Now:yyyyMMddHHmmss}.txt";
                string filePath = Path.Combine(downloadPath, fileName);

                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("BÁO CÁO CẢNH BÁO TỒN KHO");
                    writer.WriteLine($"Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                    writer.WriteLine("==========================================");
                    writer.WriteLine();

                    if (outOfStock.Any())
                    {
                        writer.WriteLine("NGUYÊN LIỆU ĐÃ HẾT:");
                        foreach (var ing in outOfStock)
                        {
                            writer.WriteLine($"- {ing.Name}: 0 {ing.Unit} (Tối thiểu: {ing.MinQuantity} {ing.Unit})");
                        }
                        writer.WriteLine();
                    }

                    if (lowStock.Any())
                    {
                        writer.WriteLine("NGUYÊN LIỆU SẮP HẾT:");
                        foreach (var ing in lowStock)
                        {
                            decimal needed = ing.MinQuantity - ing.Quantity;
                            writer.WriteLine($"- {ing.Name}: {ing.Quantity} {ing.Unit} (Cần thêm: {needed} {ing.Unit})");
                        }
                        writer.WriteLine();
                    }

                    if (affectedDishes.Any())
                    {
                        writer.WriteLine("MÓN ĂN BỊ ẢNH HƯỞNG:");
                        foreach (var dish in affectedDishes)
                        {
                            writer.WriteLine($"- {dish.Name}");
                        }
                    }
                }

                EnhancedUI.DisplaySuccess($"Đã xuất báo cáo: {fileName}");
                Logger.Info($"Inventory warning report exported: {fileName}", "IngredientManagement");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to export inventory report", "IngredientManagement", ex);
                EnhancedUI.DisplayError($"Lỗi xuất file: {ex.Message}");
            }
        }

        private void CreateIngredientOrder()
        {
            const string DOWNLOAD_FOLDER = "Downloads";

            EnhancedUI.DisplayHeader("📦 ĐẶT HÀNG NGUYÊN LIỆU");

            // Lấy các nguyên liệu low stock
            var lowStockIngredients = repository.Ingredients.Values
                .Where(ing => ing.IsLowStock)
                .ToList();

            if (!lowStockIngredients.Any())
            {
                EnhancedUI.DisplaySuccess("🎉 Không có nguyên liệu nào cần đặt hàng!");
                Console.WriteLine("Nhấn phím bất kỳ để quay lại menu...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("DANH SÁCH NGUYÊN LIỆU CẦN ĐẶT:");
            decimal totalCost = 0;
            var ingredientsToOrder = new List<(Ingredient ingredient, decimal quantity)>();

            foreach (var ing in lowStockIngredients)
            {
                decimal needed = ing.MinQuantity * 2 - ing.Quantity;
                if (needed > 0)
                {
                    decimal cost = needed * ing.PricePerUnit;
                    totalCost += cost;
                    ingredientsToOrder.Add((ing, needed));

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"- {ing.Name}: {needed} {ing.Unit} - {cost:N0}đ");
                    Console.ResetColor();
                }
            }

            Console.WriteLine($"\n💰 TỔNG CHI PHÍ DỰ KIẾN: {totalCost:N0}đ");

            if (!EnhancedUI.Confirm("Xác nhận tạo đơn đặt hàng?"))
            {
                Console.WriteLine("❌ Đã hủy tạo đơn đặt hàng.");
                Console.WriteLine("Nhấn phím bất kỳ để quay lại menu...");
                Console.ReadKey();
                return;
            }

            // Tạo đơn đặt hàng
            string orderId = $"PO_{DateTime.Now:yyyyMMddHHmmss}";
            string fileName = $"DonDatHang_{orderId}.txt";
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER, fileName);

            try
            {
                if (!Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER)))
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER));

                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("===== ĐƠN ĐẶT HÀNG NGUYÊN LIỆU =====");
                    writer.WriteLine($"Mã đơn: {orderId}");
                    writer.WriteLine($"Ngày đặt: {DateTime.Now:dd/MM/yyyy HH:mm}");
                    writer.WriteLine($"Người đặt: {currentUser.FullName}");
                    writer.WriteLine("====================================\n");

                    foreach (var (ingredient, quantity) in ingredientsToOrder)
                    {
                        writer.WriteLine($"{ingredient.Name}: {quantity} {ingredient.Unit} - {ingredient.PricePerUnit:N0}đ/{ingredient.Unit}");
                    }

                    writer.WriteLine($"\nTỔNG CỘNG: {totalCost:N0}đ");
                }

                EnhancedUI.DisplaySuccess($"✅ Đã tạo đơn đặt hàng: {fileName}");

                // THÊM PHẦN XÁC NHẬN ĐƠN HÀNG ĐÃ NHẬP VỀ
                Console.WriteLine("\n--- XÁC NHẬN NHẬP HÀNG ---");
                if (EnhancedUI.Confirm("Đơn hàng đã được nhập về kho chưa?"))
                {
                    // Cập nhật số lượng nguyên liệu trong kho
                    foreach (var (ingredient, quantity) in ingredientsToOrder)
                    {
                        var oldIngredient = new Ingredient(
                            ingredient.Id, ingredient.Name, ingredient.Unit,
                            ingredient.Quantity, ingredient.MinQuantity, ingredient.PricePerUnit
                        );

                        // Cộng thêm số lượng đã đặt
                        ingredient.Quantity += quantity;
                        ingredient.LastUpdated = DateTime.Now;

                        // Ghi log và audit
                        var command = new UpdateIngredientCommand(this, oldIngredient, ingredient);
                        undoRedoService.ExecuteCommand(command);

                        Logger.Info($"Updated ingredient {ingredient.Name} quantity: +{quantity} {ingredient.Unit}", "Inventory");
                    }

                    EnhancedUI.DisplaySuccess($"✅ Đã cập nhật tồn kho cho {ingredientsToOrder.Count} nguyên liệu!");

                    repository.AuditLogs.Add(new AuditLog(
                        currentUser.Username,
                        "CONFIRM_PURCHASE_ORDER",
                        "SYSTEM",
                        orderId,
                        $"Xác nhận nhập hàng - {ingredientsToOrder.Count} nguyên liệu - {totalCost:N0}đ"
                    ));
                }
                else
                {
                    // Chỉ ghi log tạo đơn hàng
                    repository.AuditLogs.Add(new AuditLog(
                        currentUser.Username,
                        "CREATE_PURCHASE_ORDER",
                        "SYSTEM",
                        orderId,
                        $"Tạo đơn đặt hàng - {ingredientsToOrder.Count} nguyên liệu - {totalCost:N0}đ"
                    ));

                    EnhancedUI.DisplayInfo("📋 Đơn hàng đã được tạo nhưng chưa xác nhận nhập kho.");
                    EnhancedUI.DisplayInfo("Khi hàng về, hãy sử dụng chức năng 'Nhập kho' để cập nhật số lượng.");
                }

                SaveAllData();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create purchase order", "IngredientManagement", ex);
                EnhancedUI.DisplayError($"❌ Lỗi tạo đơn đặt hàng: {ex.Message}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để quay lại menu...");
            Console.ReadKey();
        }




        // ==================== ENHANCED UNDO/REDO MENU ====================
        private void ShowUndoRedoMenu()
        {
            var menuOptions = new List<string>
            {
                "Undo (Hoàn tác)",
                "Redo (Làm lại)",
                "Xem lịch sử Undo",
                "Xem lịch sử Redo",
                "Xóa lịch sử",
                "Thống kê hoạt động"
                
            };

            while (true)
            {
                EnhancedUI.DisplayHeader("QUẢN LÝ UNDO/REDO");

                // Hiển thị trạng thái hiện tại
                Console.WriteLine($"🔄 Undo: {undoRedoService.UndoCount} lệnh có thể hoàn tác");
                Console.WriteLine($"🔁 Redo: {undoRedoService.RedoCount} lệnh có thể làm lại");
                Console.WriteLine();

                int choice = EnhancedUI.ShowMenu("UNDO/REDO & LỊCH SỬ", menuOptions, false);
                if (choice == 0) return;

                switch (choice)
                {
                    case 1: UndoLastCommand(); break;
                    case 2: RedoLastCommand(); break;
                    case 3: ShowUndoHistory(); break;
                    case 4: ShowRedoHistory(); break;
                    case 5: ClearHistory(); break;
                    case 6: ShowActivityStats(); break;
                }
            }
        }

        private void UndoLastCommand()
        {
            if (undoRedoService.CanUndo)
            {
                try
                {
                    string nextAction = undoRedoService.GetUndoHistory().FirstOrDefault();
                    if (EnhancedUI.Confirm($"Hoàn tác: {nextAction}?"))
                    {
                        undoRedoService.Undo();
                        EnhancedUI.DisplaySuccess("Đã hoàn tác thành công!");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Undo failed", "UndoRedo", ex);
                    EnhancedUI.DisplayError($"Lỗi khi hoàn tác: {ex.Message}");
                }
            }
            else
            {
                EnhancedUI.DisplayWarning("Không có lệnh nào để hoàn tác!");
            }
        }

        private void RedoLastCommand()
        {
            if (undoRedoService.CanRedo)
            {
                try
                {
                    string nextAction = undoRedoService.GetRedoHistory().FirstOrDefault();
                    if (EnhancedUI.Confirm($"Làm lại: {nextAction}?"))
                    {
                        undoRedoService.Redo();
                        EnhancedUI.DisplaySuccess("Đã làm lại thành công!");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Redo failed", "UndoRedo", ex);
                    EnhancedUI.DisplayError($"Lỗi khi làm lại: {ex.Message}");
                }
            }
            else
            {
                EnhancedUI.DisplayWarning("Không có lệnh nào để làm lại!");
            }
        }

        private void ShowUndoHistory()
        {
            EnhancedUI.DisplayHeader("LỊCH SỬ UNDO");

            var history = undoRedoService.GetUndoHistory();
            if (!history.Any())
            {
                EnhancedUI.DisplayInfo("Chưa có lịch sử undo");
                return;
            }

            Console.WriteLine("Các lệnh có thể hoàn tác:");
            for (int i = 0; i < history.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {history[i]}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ShowRedoHistory()
        {
            EnhancedUI.DisplayHeader("LỊCH SỬ REDO");

            var history = undoRedoService.GetRedoHistory();
            if (!history.Any())
            {
                EnhancedUI.DisplayInfo("Chưa có lịch sử redo");
                return;
            }

            Console.WriteLine("Các lệnh có thể làm lại:");
            for (int i = 0; i < history.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {history[i]}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ClearHistory()
        {
            if (EnhancedUI.Confirm("Xóa toàn bộ lịch sử Undo/Redo?"))
            {
                undoRedoService.Clear();
                EnhancedUI.DisplaySuccess("Đã xóa lịch sử thành công!");
            }
        }

        private void ShowActivityStats()
        {
            EnhancedUI.DisplayHeader("THỐNG KÊ HOẠT ĐỘNG");

            var today = DateTime.Today;
            var recentActivities = repository.AuditLogs
                .Where(a => a.Timestamp >= today.AddDays(-7))
                .GroupBy(a => a.Timestamp.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToList();

            Console.WriteLine("HOẠT ĐỘNG 7 NGÀY QUA:");
            foreach (var activity in recentActivities)
            {
                Console.WriteLine($"- {activity.Date:dd/MM}: {activity.Count} hoạt động");
            }

            var topUsers = repository.AuditLogs
                .Where(a => a.Timestamp >= today.AddDays(-30))
                .GroupBy(a => a.Username)
                .Select(g => new { User = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToList();

            Console.WriteLine("\nTOP NGƯỜI DÙNG NĂNG ĐỘNG:");
            foreach (var user in topUsers)
            {
                Console.WriteLine($"- {user.User}: {user.Count} hoạt động");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        // ==================== SYSTEM SETTINGS MENU ====================
        private void ShowSystemSettingsMenu()
        {
            var menuOptions = new List<string>
    {
        "Thông tin hệ thống",
        "Quản lý bộ nhớ",
        "Tối ưu hóa dữ liệu",
        "Sao lưu dữ liệu",
        "Khôi phục dữ liệu",
        "Xem logs hệ thống",
        "Xuất logs",
        "Cài đặt hiệu suất",
        "Đổi mật khẩu",
        "Đăng xuất",
        "Thoát hệ thống"
    };

            while (true)
            {
                int choice = EnhancedUI.ShowMenu("HỆ THỐNG & CÀI ĐẶT", menuOptions);
                if (choice == 0) return;

                try
                {
                    switch (choice)
                    {
                        case 1: ShowSystemInfo(); break;
                        case 2: ManageMemory(); break;
                        case 3: OptimizeData(); break;
                        case 4: BackupData(); break;
                        case 5: RestoreData(); break;
                        case 6: ShowSystemLogs(); break;
                        case 7: ExportSystemLogs(); break;
                        case 8: PerformanceSettings(); break;
                        case 9: ChangePassword(); break;
                        case 10:
                            currentUser = null;
                            return;
                        case 11:
                            if (EnhancedUI.Confirm("Xác nhận thoát hệ thống?"))
                            {
                                isRunning = false;
                                return;
                            }
                            break;
                        default:
                            EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                            Thread.Sleep(1000);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error in system settings menu: {ex.Message}", "SystemSettings", ex);
                    EnhancedUI.DisplayError($"Lỗi: {ex.Message}");
                    Thread.Sleep(2000);
                }
            }
        }

        private void OptimizeData()
{
    EnhancedUI.DisplayHeader("TỐI ƯU HÓA DỮ LIỆU");

    try
    {
        Console.WriteLine("Đang phân tích dữ liệu...");
        Thread.Sleep(1000);

        string performanceStats = memoryManager.GetPerformanceStats();
        Console.WriteLine(performanceStats);

        Console.WriteLine("\nTùy chọn tối ưu hóa:");
        Console.WriteLine("1. Tối ưu datasets lớn");
        Console.WriteLine("2. Nén dữ liệu");
        Console.WriteLine("3. Dọn dẹp toàn bộ");
        Console.Write("Chọn: ");

        string choice = Console.ReadLine();
        switch (choice)
        {
            case "1":
                memoryManager.OptimizeLargeDatasets();
                EnhancedUI.DisplaySuccess("Đã tối ưu hóa datasets lớn!");
                break;
            case "2":
                memoryManager.CompactData();
                EnhancedUI.DisplaySuccess("Đã nén dữ liệu!");
                break;
            case "3":
                memoryManager.OptimizeLargeDatasets();
                memoryManager.CompactData();
                memoryManager.Cleanup();
                EnhancedUI.DisplaySuccess("Đã dọn dẹp và tối ưu toàn bộ hệ thống!");
                break;
            default:
                EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                break;
        }

        // Hiển thị thống kê sau khi tối ưu
        Console.WriteLine("\n" + memoryManager.GetPerformanceStats());
    }
    catch (Exception ex)
    {
        Logger.Error("Data optimization failed", "SystemSettings", ex);
        EnhancedUI.DisplayError($"Lỗi tối ưu hóa: {ex.Message}");
    }

    Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
    Console.ReadKey();
}

        private void ShowSystemInfo()
        {
            EnhancedUI.DisplayHeader("THÔNG TIN HỆ THỐNG");

            var process = Process.GetCurrentProcess();
            var startTime = process.StartTime;
            var uptime = DateTime.Now - startTime;

            Console.WriteLine($"🖥️  Phiên bản: 2.0 Professional");
            Console.WriteLine($"👤 Người dùng: {currentUser.FullName} ({currentUser.Role})");
            Console.WriteLine($"⏰ Thời gian chạy: {uptime:dd\\.hh\\:mm\\:ss}");
            Console.WriteLine($"📊 {memoryManager.GetMemoryInfo()}");
            Console.WriteLine($"💾 Dung lượng dữ liệu:");
            Console.WriteLine($"   - Món ăn: {repository.Dishes.Count}");
            Console.WriteLine($"   - Nguyên liệu: {repository.Ingredients.Count}");
            Console.WriteLine($"   - Combo: {repository.Combos.Count}");
            Console.WriteLine($"   - Đơn hàng: {repository.Orders.Count}");
            Console.WriteLine($"   - Người dùng: {repository.Users.Count}");

            // Hiển thị hiệu suất
            var cpuUsage = GetCpuUsage();
            Console.WriteLine($"⚡ Hiệu suất: CPU ~{cpuUsage}%");

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private string GetCpuUsage()
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;

                Thread.Sleep(500);

                var endTime = DateTime.UtcNow;
                var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;

                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

                return (cpuUsageTotal * 100).ToString("F1");
            }
            catch
            {
                return "N/A";
            }
        }

        private void ManageMemory()
        {
            EnhancedUI.DisplayHeader("QUẢN LÝ BỘ NHỚ");

            Console.WriteLine(memoryManager.GetMemoryInfo());
            Console.WriteLine();

            if (EnhancedUI.Confirm("Chạy dọn dẹp bộ nhớ ngay bây giờ?"))
            {
                memoryManager.Cleanup();
                EnhancedUI.DisplaySuccess("Đã dọn dẹp bộ nhớ thành công!");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void PerformanceSettings()
        {
            EnhancedUI.DisplayHeader("CÀI ĐẶT HIỆU SUẤT");

            Console.WriteLine("1. Ưu tiên hiệu suất (sử dụng nhiều RAM hơn)");
            Console.WriteLine("2. Ưu tiên bộ nhớ (giảm sử dụng RAM)");
            Console.WriteLine("3. Cân bằng (mặc định)");
            Console.Write("Chọn chế độ: ");

            string choice = Console.ReadLine();
            switch (choice)
            {
                case "1":
                    EnhancedUI.DisplaySuccess("Đã đặt ưu tiên hiệu suất cao");
                    break;
                case "2":
                    memoryManager.OptimizeLargeDatasets();
                    EnhancedUI.DisplaySuccess("Đã đặt ưu tiên tiết kiệm bộ nhớ");
                    break;
                case "3":
                    EnhancedUI.DisplayInfo("Giữ chế độ cân bằng mặc định");
                    break;
                default:
                    EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                    break;
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ShowSystemLogs()
        {
            EnhancedUI.DisplayHeader("LOGS HỆ THỐNG");

            var logs = Logger.GetLogs(count: 20);
            if (!logs.Any())
            {
                EnhancedUI.DisplayInfo("Chưa có logs hệ thống");
                return;
            }

            foreach (var log in logs)
            {
                ConsoleColor color = ConsoleColor.White;
                switch (log.Level)
                {
                    case LogLevel.INFO: color = ConsoleColor.Cyan; break;
                    case LogLevel.WARNING: color = ConsoleColor.Yellow; break;
                    case LogLevel.ERROR: color = ConsoleColor.Red; break;
                    case LogLevel.DEBUG: color = ConsoleColor.Gray; break;
                }

                Console.ForegroundColor = color;
                Console.WriteLine($"[{log.Timestamp:HH:mm:ss}] [{log.Level}] {log.Module}: {log.Message}");
                if (!string.IsNullOrEmpty(log.Exception))
                {
                    Console.WriteLine($"      Exception: {log.Exception}");
                }
                Console.ResetColor();
            }

            Console.WriteLine($"\nHiển thị {logs.Count} logs gần nhất");
            Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ExportSystemLogs()
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"SystemLogs_{DateTime.Now:yyyyMMddHHmmss}.csv";
                string filePath = Path.Combine(downloadPath, fileName);

                Logger.ExportLogs(filePath);
                EnhancedUI.DisplaySuccess($"Đã xuất logs hệ thống: {fileName}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to export system logs", "System", ex);
                EnhancedUI.DisplayError($"Lỗi xuất logs: {ex.Message}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        // ==================== CORE BUSINESS METHODS ====================
        public bool DeductIngredients(Order order)
        {
            try
            {
                // Kiểm tra trước khi trừ
                foreach (var item in order.Items)
                {
                    if (!item.IsCombo)
                    {
                        if (repository.Dishes.ContainsKey(item.ItemId))
                        {
                            var dish = repository.Dishes[item.ItemId];
                            foreach (var ing in dish.Ingredients)
                            {
                                if (!repository.Ingredients.ContainsKey(ing.Key) ||
                                    repository.Ingredients[ing.Key].Quantity < ing.Value * item.Quantity)
                                {
                                    return false;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (repository.Combos.ContainsKey(item.ItemId))
                        {
                            var combo = repository.Combos[item.ItemId];
                            foreach (var dishId in combo.DishIds)
                            {
                                if (repository.Dishes.ContainsKey(dishId))
                                {
                                    var dish = repository.Dishes[dishId];
                                    foreach (var ing in dish.Ingredients)
                                    {
                                        if (!repository.Ingredients.ContainsKey(ing.Key) ||
                                            repository.Ingredients[ing.Key].Quantity < ing.Value * item.Quantity)
                                        {
                                            return false;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Thực hiện trừ nguyên liệu
                foreach (var item in order.Items)
                {
                    if (!item.IsCombo)
                    {
                        if (repository.Dishes.ContainsKey(item.ItemId))
                        {
                            var dish = repository.Dishes[item.ItemId];
                            foreach (var ing in dish.Ingredients)
                            {
                                repository.Ingredients[ing.Key].Quantity -= ing.Value * item.Quantity;
                                repository.Ingredients[ing.Key].LastUpdated = DateTime.Now;
                            }
                        }
                    }
                    else
                    {
                        if (repository.Combos.ContainsKey(item.ItemId))
                        {
                            var combo = repository.Combos[item.ItemId];
                            foreach (var dishId in combo.DishIds)
                            {
                                if (repository.Dishes.ContainsKey(dishId))
                                {
                                    var dish = repository.Dishes[dishId];
                                    foreach (var ing in dish.Ingredients)
                                    {
                                        repository.Ingredients[ing.Key].Quantity -= ing.Value * item.Quantity;
                                        repository.Ingredients[ing.Key].LastUpdated = DateTime.Now;
                                    }
                                }
                            }
                        }
                    }
                }

                Logger.Info($"Ingredients deducted for order {order.Id}", "OrderManagement");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to deduct ingredients for order {order.Id}", "OrderManagement", ex);
                return false;
            }
        }

        private bool CheckDishIngredients(Dish dish)
        {
            foreach (var ing in dish.Ingredients)
            {
                if (!repository.Ingredients.ContainsKey(ing.Key) ||
                    repository.Ingredients[ing.Key].Quantity < ing.Value)
                {
                    return false;
                }
            }
            return true;
        }

        private void ChangePassword()
        {
            EnhancedUI.DisplayHeader("ĐỔI MẬT KHẨU");

            string currentPassword = EnhancedUI.ReadPassword("Mật khẩu hiện tại: ");
            if (!SecurityService.VerifyPassword(currentPassword, currentUser.PasswordHash))
            {
                EnhancedUI.DisplayError("Mật khẩu hiện tại không đúng!");
                return;
            }

            string newPassword = EnhancedUI.ReadPassword("Mật khẩu mới: ");
            string confirmPassword = EnhancedUI.ReadPassword("Xác nhận mật khẩu mới: ");

            if (newPassword != confirmPassword)
            {
                EnhancedUI.DisplayError("Mật khẩu xác nhận không khớp!");
                return;
            }

            if (newPassword.Length < 6)
            {
                EnhancedUI.DisplayError("Mật khẩu phải có ít nhất 6 ký tự!");
                return;
            }

            currentUser.PasswordHash = SecurityService.HashPassword(newPassword);
            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "CHANGE_PASSWORD", "USER", currentUser.Username, "Đổi mật khẩu"));
            SaveAllData();

            EnhancedUI.DisplaySuccess("Đổi mật khẩu thành công!");
            Logger.Info($"User {currentUser.Username} changed password", "Authentication");

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        // ==================== DATA PERSISTENCE ====================
        private void LoadAllData()
        {
            Logger.Info("Loading all data", "DataPersistence");

            repository.Users = LoadData<Dictionary<string, User>>("users.json") ?? new Dictionary<string, User>();
            repository.Ingredients = LoadData<Dictionary<string, Ingredient>>("ingredients.json") ?? new Dictionary<string, Ingredient>();
            repository.Dishes = LoadData<Dictionary<string, Dish>>("dishes.json") ?? new Dictionary<string, Dish>();
            repository.Combos = LoadData<Dictionary<string, Combo>>("combos.json") ?? new Dictionary<string, Combo>();
            repository.Orders = LoadData<Dictionary<string, Order>>("orders.json") ?? new Dictionary<string, Order>();
            repository.AuditLogs = LoadData<List<AuditLog>>("audit_logs.json") ?? new List<AuditLog>();

            // Tạo dữ liệu mẫu nếu chưa có
            if (repository.Users.Count == 0) CreateSampleUsers();
            if (repository.Ingredients.Count == 0) CreateSampleIngredients();
            if (repository.Dishes.Count == 0) CreateSampleDishes();

            Logger.Info("Data loading completed", "DataPersistence");
        }

        private T LoadData<T>(string fileName)
        {
            try
            {
                string filePath = Path.Combine(DATA_FOLDER, fileName);
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    return JsonConvert.DeserializeObject<T>(json);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load {fileName}: {ex.Message}", "DataPersistence", ex);
            }
            return default(T);
        }

        public void SaveAllData()
        {
            Logger.Info("Saving all data", "DataPersistence");

            SaveData("users.json", repository.Users);
            SaveData("ingredients.json", repository.Ingredients);
            SaveData("dishes.json", repository.Dishes);
            SaveData("combos.json", repository.Combos);
            SaveData("orders.json", repository.Orders);
            SaveData("audit_logs.json", repository.AuditLogs);

            Logger.Info("Data saving completed", "DataPersistence");
        }

        private void SaveData<T>(string fileName, T data)
        {
            try
            {
                string filePath = Path.Combine(DATA_FOLDER, fileName);

                // Đảm bảo thư mục tồn tại
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(filePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save {fileName}: {ex.Message}", "DataPersistence", ex);
                throw; // Re-throw để xử lý ở tầng trên
            }
        }

        // ==================== FIXED BACKUP SYSTEM ====================
        private void BackupData()
        {
            try
            {
                EnhancedUI.DisplayHeader("📦 SAO LƯU DỮ LIỆU");

                Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                   ĐANG SAO LƯU DỮ LIỆU...                    ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

                // Đảm bảo thư mục backup tồn tại
                if (!Directory.Exists(BACKUP_FOLDER))
                {
                    Directory.CreateDirectory(BACKUP_FOLDER);
                }

                string backupDirName = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupDir = Path.Combine(BACKUP_FOLDER, backupDirName);

                // Tạo thư mục backup với đầy đủ path
                Console.WriteLine("║                                                                ║");
                Console.WriteLine($"║   📁 Tạo thư mục backup...                                   ");
                try
                {
                    Directory.CreateDirectory(backupDir);
                    EnhancedUI.DisplaySuccess($"   ✅ Đã tạo: {backupDir}");
                }
                catch (Exception ex)
                {
                    EnhancedUI.DisplayError($"   ❌ Lỗi tạo thư mục: {ex.Message}");
                    throw;
                }
                EnhancedUI.DisplayProgressBar(1, 6, 50);
                Thread.Sleep(300);

                // Sao lưu từng file với error handling
                bool allSuccess = true;
                List<string> backupResults = new List<string>();

                Console.WriteLine("║                                                                ║");
                Console.WriteLine($"║   👥 Sao lưu người dùng...                                   ");
                if (SaveDataWithRetry(Path.Combine(backupDir, "users.json"), repository.Users))
                {
                    EnhancedUI.DisplaySuccess($"   ✅ {repository.Users.Count} người dùng");
                    backupResults.Add($"👥 Người dùng: {repository.Users.Count}");
                }
                else
                {
                    EnhancedUI.DisplayError("   ❌ Thất bại");
                    allSuccess = false;
                }
                EnhancedUI.DisplayProgressBar(2, 6, 50);
                Thread.Sleep(300);

                Console.WriteLine("║                                                                ║");
                Console.WriteLine($"║   🥬 Sao lưu nguyên liệu...                                  ");
                if (SaveDataWithRetry(Path.Combine(backupDir, "ingredients.json"), repository.Ingredients))
                {
                    EnhancedUI.DisplaySuccess($"   ✅ {repository.Ingredients.Count} nguyên liệu");
                    backupResults.Add($"🥬 Nguyên liệu: {repository.Ingredients.Count}");
                }
                else
                {
                    EnhancedUI.DisplayError("   ❌ Thất bại");
                    allSuccess = false;
                }
                EnhancedUI.DisplayProgressBar(3, 6, 50);
                Thread.Sleep(300);

                Console.WriteLine("║                                                                ║");
                Console.WriteLine($"║   🍽️  Sao lưu món ăn...                                      ");
                if (SaveDataWithRetry(Path.Combine(backupDir, "dishes.json"), repository.Dishes))
                {
                    EnhancedUI.DisplaySuccess($"   ✅ {repository.Dishes.Count} món ăn");
                    backupResults.Add($"🍽️ Món ăn: {repository.Dishes.Count}");
                }
                else
                {
                    EnhancedUI.DisplayError("   ❌ Thất bại");
                    allSuccess = false;
                }
                EnhancedUI.DisplayProgressBar(4, 6, 50);
                Thread.Sleep(300);

                Console.WriteLine("║                                                                ║");
                Console.WriteLine($"║   🎁 Sao lưu combo...                                        ");
                if (SaveDataWithRetry(Path.Combine(backupDir, "combos.json"), repository.Combos))
                {
                    EnhancedUI.DisplaySuccess($"   ✅ {repository.Combos.Count} combo");
                    backupResults.Add($"🎁 Combo: {repository.Combos.Count}");
                }
                else
                {
                    EnhancedUI.DisplayError("   ❌ Thất bại");
                    allSuccess = false;
                }
                EnhancedUI.DisplayProgressBar(5, 6, 50);
                Thread.Sleep(300);

                Console.WriteLine("║                                                                ║");
                Console.WriteLine($"║   📋 Sao lưu đơn hàng & logs...                              ");
                bool ordersSuccess = SaveDataWithRetry(Path.Combine(backupDir, "orders.json"), repository.Orders);
                bool logsSuccess = SaveDataWithRetry(Path.Combine(backupDir, "audit_logs.json"), repository.AuditLogs);

                if (ordersSuccess && logsSuccess)
                {
                    EnhancedUI.DisplaySuccess($"   ✅ {repository.Orders.Count} đơn hàng, {repository.AuditLogs.Count} logs");
                    backupResults.Add($"📋 Đơn hàng: {repository.Orders.Count}");
                    backupResults.Add($"📝 Audit logs: {repository.AuditLogs.Count}");
                }
                else
                {
                    EnhancedUI.DisplayError("   ❌ Thất bại một phần");
                    allSuccess = false;
                }
                EnhancedUI.DisplayProgressBar(6, 6, 50);

                Console.WriteLine("║                                                                ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

                // Hiển thị kết quả
                Console.WriteLine("\n📊 KẾT QUẢ SAO LƯU:");
                foreach (var result in backupResults)
                {
                    Console.WriteLine($"   • {result}");
                }

                Console.WriteLine($"   • 📁 Thư mục: {backupDir}");
                Console.WriteLine($"   • ⏰ Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");

                if (allSuccess)
                {
                    EnhancedUI.DisplaySuccess($"\n✅ SAO LƯU HOÀN TẤT THÀNH CÔNG!");
                    Logger.Info($"Data backed up successfully to {backupDir}", "Backup");
                }
                else
                {
                    EnhancedUI.DisplayWarning($"\n⚠️ SAO LƯU HOÀN TẤT VỚI MỘT SỐ LỖI");
                    Logger.Warning($"Data backed up with some errors to {backupDir}", "Backup");
                }

                // Chờ nhấn phím
                Console.WriteLine("\n" + new string('═', 64));
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("👉 NHẤN PHÍM BẤT KỲ ĐỂ TIẾP TỤC...");
                Console.ResetColor();
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                EnhancedUI.DisplayError($"\n❌ LỖI SAO LƯU: {ex.Message}");
                Logger.Error($"Backup failed: {ex.Message}", "Backup", ex);

                Console.WriteLine("\n" + new string('═', 64));
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("👉 NHẤN PHÍM BẤT KỲ ĐỂ TIẾP TỤC...");
                Console.ResetColor();
                Console.ReadKey();
            }
        }

        // ==================== IMPROVED SAVE DATA METHOD ====================
        private bool SaveDataWithRetry<T>(string filePath, T data, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Đảm bảo thư mục tồn tại
                    string directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Serialize và lưu dữ liệu
                    string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    File.WriteAllText(filePath, json, Encoding.UTF8);

                    // Xác minh file đã được ghi
                    if (File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.Length > 0)
                        {
                            return true;
                        }
                    }

                    // Nếu file không tồn tại hoặc rỗng, thử lại
                    if (attempt < maxRetries)
                    {
                        Thread.Sleep(100 * attempt);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Save attempt {attempt} failed for {filePath}: {ex.Message}", "Backup");

                    if (attempt < maxRetries)
                    {
                        Thread.Sleep(100 * attempt);
                        continue;
                    }
                }
            }

            return false;
        }

        private void RestoreData()
        {
            try
            {
                EnhancedUI.DisplayHeader("🔄 KHÔI PHỤC DỮ LIỆU");

                // Kiểm tra thư mục backup
                if (!Directory.Exists(BACKUP_FOLDER))
                {
                    EnhancedUI.DisplayError("❌ Thư mục backup không tồn tại!");
                    Console.WriteLine("   Vui lòng tạo bản sao lưu trước khi khôi phục.");
                    WaitForAnyKey();
                    return;
                }

                // Lấy danh sách backup
                var backupDirs = GetValidBackupDirectories();

                if (!backupDirs.Any())
                {
                    EnhancedUI.DisplayError("❌ Không tìm thấy bản backup hợp lệ nào!");
                    WaitForAnyKey();
                    return;
                }

                // Hiển thị danh sách backup
                DisplayBackupList(backupDirs);

                Console.Write("\nChọn bản backup để khôi phục (số): ");
                string input = Console.ReadLine();

                if (input == "0")
                {
                    EnhancedUI.DisplayInfo("⏹️  Đã hủy thao tác khôi phục.");
                    WaitForAnyKey();
                    return;
                }

                if (int.TryParse(input, out int choice) && choice > 0 && choice <= backupDirs.Count)
                {
                    var backupInfo = backupDirs[choice - 1];
                    PerformRestore(backupInfo);
                }
                else
                {
                    EnhancedUI.DisplayError("❌ Lựa chọn không hợp lệ!");
                }

                WaitForAnyKey();
            }
            catch (Exception ex)
            {
                EnhancedUI.DisplayError($"❌ Lỗi khôi phục: {ex.Message}");
                Logger.Error($"Restore failed: {ex.Message}", "Restore", ex);
                WaitForAnyKey();
            }
        }

        private void WaitForAnyKey()
        {
            Console.WriteLine("\n" + new string('═', 64));
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("👉 NHẤN PHÍM BẤT KỲ ĐỂ TIẾP TỤC...");
            Console.ResetColor();
            Console.ReadKey();
        }

        private void PerformRestore(BackupInfo backupInfo)
        {
            Console.WriteLine($"\n📋 THÔNG TIN BẢN BACKUP:");
            Console.WriteLine($"   • Tên: {backupInfo.Name}");
            Console.WriteLine($"   • Thời gian: {backupInfo.Created:dd/MM/yyyy HH:mm:ss}");
            Console.WriteLine($"   • Kích thước: {backupInfo.Size}");
            Console.WriteLine($"   • Đường dẫn: {backupInfo.Path}");

            // Xác nhận khôi phục
            Console.WriteLine($"\n⚠️  CẢNH BÁO QUAN TRỌNG:");
            Console.WriteLine($"   • Dữ liệu hiện tại sẽ bị GHI ĐÈ hoàn toàn");
            Console.WriteLine($"   • Thao tác này KHÔNG THỂ HOÀN TÁC");

            if (!EnhancedUI.Confirm("Bạn có CHẮC CHẮN muốn khôi phục từ bản backup này?"))
            {
                EnhancedUI.DisplayInfo("⏹️  Đã hủy thao tác khôi phục.");
                return;
            }

            // Tạo emergency backup trước
            CreateEmergencyBackup();

            // Thực hiện khôi phục
            if (ExecuteRestore(backupInfo.Path))
            {
                EnhancedUI.DisplaySuccess($"✅ Khôi phục dữ liệu thành công từ {backupInfo.Name}!");
                Logger.Info($"Data restored from {backupInfo.Path}", "Restore");

                repository.AuditLogs.Add(new AuditLog(
                    currentUser?.Username ?? "SYSTEM",
                    "RESTORE_DATA",
                    "SYSTEM",
                    "",
                    $"Khôi phục dữ liệu từ backup: {backupInfo.Name}"
                ));

                SaveAllData(); // Lưu audit log
            }
            else
            {
                EnhancedUI.DisplayError("❌ Khôi phục thất bại!");
            }
        }

        private void CreateEmergencyBackup()
        {
            try
            {
                string emergencyBackupDir = Path.Combine(BACKUP_FOLDER, $"EMERGENCY_BACKUP_{DateTime.Now:yyyyMMdd_HHmmss}");
                Directory.CreateDirectory(emergencyBackupDir);

                SaveData(Path.Combine(emergencyBackupDir, "users.json"), repository.Users);
                SaveData(Path.Combine(emergencyBackupDir, "ingredients.json"), repository.Ingredients);
                SaveData(Path.Combine(emergencyBackupDir, "dishes.json"), repository.Dishes);
                SaveData(Path.Combine(emergencyBackupDir, "combos.json"), repository.Combos);
                SaveData(Path.Combine(emergencyBackupDir, "orders.json"), repository.Orders);
                SaveData(Path.Combine(emergencyBackupDir, "audit_logs.json"), repository.AuditLogs);

                EnhancedUI.DisplaySuccess($"✅ Đã tạo bản sao lưu khẩn cấp: {Path.GetFileName(emergencyBackupDir)}");
            }
            catch (Exception ex)
            {
                EnhancedUI.DisplayWarning($"⚠️  Không thể tạo bản sao lưu khẩn cấp: {ex.Message}");
            }
        }

        private bool ExecuteRestore(string backupDir)
        {
            try
            {
                Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                   ĐANG KHÔI PHỤC DỮ LIỆU...                  ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

                var steps = new[]
                {
            new { Name = "👥 Khôi phục người dùng...", File = "users.json", Action = new Action<string>(path =>
                repository.Users = LoadData<Dictionary<string, User>>(path) ?? new Dictionary<string, User>()) },
            new { Name = "🥬 Khôi phục nguyên liệu...", File = "ingredients.json", Action = new Action<string>(path =>
                repository.Ingredients = LoadData<Dictionary<string, Ingredient>>(path) ?? new Dictionary<string, Ingredient>()) },
            new { Name = "🍽️  Khôi phục món ăn...", File = "dishes.json", Action = new Action<string>(path =>
                repository.Dishes = LoadData<Dictionary<string, Dish>>(path) ?? new Dictionary<string, Dish>()) },
            new { Name = "🎁 Khôi phục combo...", File = "combos.json", Action = new Action<string>(path =>
                repository.Combos = LoadData<Dictionary<string, Combo>>(path) ?? new Dictionary<string, Combo>()) },
            new { Name = "📋 Khôi phục đơn hàng...", File = "orders.json", Action = new Action<string>(path =>
                repository.Orders = LoadData<Dictionary<string, Order>>(path) ?? new Dictionary<string, Order>()) },
            new { Name = "📝 Khôi phục audit logs...", File = "audit_logs.json", Action = new Action<string>(path =>
                repository.AuditLogs = LoadData<List<AuditLog>>(path) ?? new List<AuditLog>()) }
        };

                for (int i = 0; i < steps.Length; i++)
                {
                    var step = steps[i];
                    Console.WriteLine("║                                                                ║");
                    Console.WriteLine($"║   {step.Name}                                ");

                    string filePath = Path.Combine(backupDir, step.File);
                    if (File.Exists(filePath))
                    {
                        step.Action(filePath);
                        EnhancedUI.DisplaySuccess("   ✅ Thành công");
                    }
                    else
                    {
                        EnhancedUI.DisplayWarning("   ⚠️  File không tồn tại, tạo mới");
                    }

                    EnhancedUI.DisplayProgressBar(i + 1, steps.Length, 50);
                    Thread.Sleep(200);
                }

                Console.WriteLine("║                                                                ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Restore execution failed: {ex.Message}", "Restore", ex);
                return false;
            }
        }

        private void DisplayBackupList(List<BackupInfo> backups)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                   DANH SÁCH BẢN BACKUP                       ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║                                                                ║");

            for (int i = 0; i < backups.Count; i++)
            {
                var backup = backups[i];
                Console.WriteLine($"║   {i + 1,2}. {backup.Name,-20} {backup.Created:dd/MM HH:mm} {backup.Size,-15} ║");
            }

            Console.WriteLine("║                                                                ║");
            Console.WriteLine("║   0. Hủy bỏ                                                    ║");
            Console.WriteLine("║                                                                ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        }

        private class BackupInfo
        {
            public string Path { get; set; }
            public string Name { get; set; }
            public string Size { get; set; }
            public DateTime Created { get; set; }
        }

        private List<BackupInfo> GetValidBackupDirectories()
        {
            var validBackups = new List<BackupInfo>();

            try
            {
                var backupDirs = Directory.GetDirectories(BACKUP_FOLDER)
                    .OrderByDescending(d => d)
                    .Take(10);

                foreach (var dir in backupDirs)
                {
                    if (ValidateBackupIntegrity(dir))
                    {
                        validBackups.Add(new BackupInfo
                        {
                            Path = dir,
                            Name = Path.GetFileName(dir),
                            Size = GetBackupSizeInfo(dir),
                            Created = ExtractDateTimeFromBackupName(Path.GetFileName(dir))
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting backup directories: {ex.Message}", "Restore", ex);
            }

            return validBackups;
        }

        private bool ValidateBackupIntegrity(string backupDir)
        {
            try
            {
                if (!Directory.Exists(backupDir))
                {
                    Logger.Warning($"Backup directory doesn't exist: {backupDir}", "BackupValidation");
                    return false;
                }

                // Kiểm tra các file quan trọng
                string[] requiredFiles = {
            "users.json", "ingredients.json", "dishes.json",
            "combos.json", "orders.json", "audit_logs.json"
        };

                foreach (string file in requiredFiles)
                {
                    string filePath = Path.Combine(backupDir, file);
                    if (!File.Exists(filePath))
                    {
                        Logger.Warning($"Backup file missing: {file}", "BackupValidation");
                        return false;
                    }

                    // Kiểm tra file có thể đọc được và có nội dung
                    try
                    {
                        string content = File.ReadAllText(filePath);
                        if (string.IsNullOrWhiteSpace(content))
                        {
                            Logger.Warning($"Backup file empty: {file}", "BackupValidation");
                            return false;
                        }

                        // Kiểm tra định dạng JSON
                        if (file.EndsWith(".json"))
                        {
                            try
                            {
                                if (file == "users.json")
                                    JsonConvert.DeserializeObject<Dictionary<string, User>>(content);
                                else if (file == "ingredients.json")
                                    JsonConvert.DeserializeObject<Dictionary<string, Ingredient>>(content);
                                else if (file == "dishes.json")
                                    JsonConvert.DeserializeObject<Dictionary<string, Dish>>(content);
                                else if (file == "combos.json")
                                    JsonConvert.DeserializeObject<Dictionary<string, Combo>>(content);
                                else if (file == "orders.json")
                                    JsonConvert.DeserializeObject<Dictionary<string, Order>>(content);
                                else if (file == "audit_logs.json")
                                    JsonConvert.DeserializeObject<List<AuditLog>>(content);
                            }
                            catch (JsonException jsonEx)
                            {
                                Logger.Warning($"Backup file has invalid JSON: {file} - {jsonEx.Message}", "BackupValidation");
                                return false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Backup file corrupted: {file} - {ex.Message}", "BackupValidation");
                        return false;
                    }
                }

                Logger.Info($"Backup validation passed: {backupDir}", "BackupValidation");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Backup validation failed: {backupDir} - {ex.Message}", "BackupValidation", ex);
                return false;
            }
        }

        private string GetBackupSizeInfo(string backupDir)
        {
            try
            {
                if (!Directory.Exists(backupDir))
                    return "N/A";

                long totalSize = 0;
                var files = Directory.GetFiles(backupDir, "*.json");
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    totalSize += fileInfo.Length;
                }

                if (totalSize == 0)
                    return "0 B";
                else if (totalSize < 1024)
                    return $"{totalSize} B";
                else if (totalSize < 1024 * 1024)
                    return $"{(totalSize / 1024.0):0.0} KB";
                else
                    return $"{(totalSize / (1024.0 * 1024.0)):0.0} MB";
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get backup size: {backupDir}", "Backup", ex);
                return "N/A";
            }
        }

        // ==================== BACKUP HELPER METHODS ====================
        private DateTime ExtractDateTimeFromBackupName(string backupName)
        {
            try
            {
                // Format: yyyyMMdd_HHmmss
                if (backupName.Length >= 15)
                {
                    string datePart = backupName.Substring(0, 8);
                    string timePart = backupName.Substring(9, 6);

                    int year = int.Parse(datePart.Substring(0, 4));
                    int month = int.Parse(datePart.Substring(4, 2));
                    int day = int.Parse(datePart.Substring(6, 2));
                    int hour = int.Parse(timePart.Substring(0, 2));
                    int minute = int.Parse(timePart.Substring(2, 2));
                    int second = int.Parse(timePart.Substring(4, 2));

                    return new DateTime(year, month, day, hour, minute, second);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse backup name: {backupName}", "Backup", ex);
            }

            // Nếu không parse được, trả về thời gian mặc định
            return Directory.GetCreationTime(Path.Combine(BACKUP_FOLDER, backupName));
        }

        // ==================== SAMPLE DATA CREATION ====================
        private void CreateSampleData()
        {
            CreateSampleUsers();
            CreateSampleIngredients();
            CreateSampleDishes();
            CreateSampleCombos();

            Logger.Info("Sample data created", "DataPersistence");
            EnhancedUI.DisplaySuccess("Đã tạo dữ liệu mẫu thành công!");
        }

        private void CreateSampleUsers()
        {
            repository.Users["admin"] = new User("admin", SecurityService.HashPassword("admin123"), UserRole.Admin, "Quản trị viên");
            repository.Users["manager"] = new User("manager", SecurityService.HashPassword("manager123"), UserRole.Manager, "Quản lý nhà hàng");
            repository.Users["staff"] = new User("staff", SecurityService.HashPassword("staff123"), UserRole.Staff, "Nhân viên phục vụ");
            repository.Users["chef"] = new User("chef", SecurityService.HashPassword("chef123"), UserRole.Manager, "Đầu bếp chính");
        }

        private void CreateSampleIngredients()
        {
            var sampleIngredients = new[]
            {
                new Ingredient("THIT001", "Thịt bò", "kg", 50, 10, 250000),
                new Ingredient("THIT002", "Thịt heo", "kg", 40, 8, 150000),
                new Ingredient("THIT003", "Thịt gà", "kg", 30, 5, 120000),
                new Ingredient("HAIS001", "Tôm", "kg", 20, 5, 300000),
                new Ingredient("HAIS002", "Cá hồi", "kg", 15, 3, 400000),
                new Ingredient("RAU001", "Rau xà lách", "bó", 100, 20, 15000),
                new Ingredient("RAU002", "Cà chua", "kg", 50, 10, 25000),
                new Ingredient("RAU003", "Hành tây", "kg", 40, 8, 20000),
                new Ingredient("RAU004", "Tỏi", "kg", 30, 5, 50000),
                new Ingredient("RAU005", "Rau mùi", "bó", 80, 15, 10000),
                new Ingredient("GAO001", "Gạo", "kg", 200, 50, 25000),
                new Ingredient("NUOC001", "Nước mắm", "chai", 50, 10, 30000),
                new Ingredient("NUOC002", "Dầu ăn", "chai", 30, 5, 40000),
                new Ingredient("NUOC003", "Nước tương", "chai", 40, 8, 25000),
                new Ingredient("GIAVI001", "Muối", "kg", 20, 5, 10000),
                new Ingredient("GIAVI002", "Đường", "kg", 25, 5, 20000),
                new Ingredient("GIAVI003", "Tiêu", "kg", 10, 2, 150000),
                new Ingredient("BOT001", "Bột mì", "kg", 60, 15, 30000),
                new Ingredient("TRUNG001", "Trứng gà", "quả", 200, 50, 3000),
                new Ingredient("SUP001", "Súp lơ", "kg", 35, 7, 35000)
            };

            foreach (var ing in sampleIngredients)
            {
                repository.Ingredients[ing.Id] = ing;
            }
        }

        private void CreateSampleDishes()
        {
            // Clear existing dishes to avoid conflicts
            repository.Dishes.Clear();

            var sampleDishes = new[]
            {
        new Dish("MON001", "Phở bò", "Phở bò truyền thống", 65000, "Món chính"),
        new Dish("MON002", "Bún chả", "Bún chả Hà Nội", 55000, "Món chính"),
        new Dish("MON003", "Cơm gà", "Cơm gà xối mỡ", 45000, "Món chính"),
        new Dish("MON004", "Bánh mì", "Bánh mì thập cẩm", 35000, "Món chính"),
        new Dish("MON005", "Gỏi cuốn", "Gỏi cuốn tôm thịt", 40000, "Món khai vị")
    };

            // Add ingredients to dishes only if ingredients exist
            if (repository.Ingredients.ContainsKey("THIT001"))
            {
                sampleDishes[0].Ingredients["THIT001"] = 0.2m;
            }
            if (repository.Ingredients.ContainsKey("RAU005"))
            {
                sampleDishes[0].Ingredients["RAU005"] = 0.1m;
            }

            if (repository.Ingredients.ContainsKey("THIT002"))
            {
                sampleDishes[1].Ingredients["THIT002"] = 0.15m;
            }
            if (repository.Ingredients.ContainsKey("RAU001"))
            {
                sampleDishes[1].Ingredients["RAU001"] = 0.1m;
            }

            foreach (var dish in sampleDishes)
            {
                dish.CalculateCost(repository.Ingredients);
                repository.Dishes[dish.Id] = dish;
            }
        }

        private void CreateSampleCombos()
        {
            // Clear existing combos
            repository.Combos.Clear();

            var combo1 = new Combo("COMBO001", "Combo Gia Đình", "Combo ấm cúng cho gia đình", 15);

            // Only add dishes that exist
            if (repository.Dishes.ContainsKey("MON001"))
                combo1.DishIds.Add("MON001");
            if (repository.Dishes.ContainsKey("MON005"))
                combo1.DishIds.Add("MON005");

            // Calculate prices after adding dishes
            combo1.CalculateOriginalPrice(repository.Dishes);
            combo1.CalculateCost(repository.Dishes);

            repository.Combos[combo1.Id] = combo1;

            Logger.Info($"Created sample combo: {combo1.Name} - {combo1.FinalPrice:N0}đ", "SampleData");
        }

        // ==================== DISPLAY METHODS ====================
        private void DisplayDishes(int page = 1, int pageSize = 19)
        {
            while (true)
            {


                EnhancedUI.DisplayHeader("🍽️ DANH SÁCH MÓN ĂN");

                var dishList = repository.Dishes.Values.ToList();
                int totalPages = (int)Math.Ceiling(dishList.Count / (double)pageSize);

                if (dishList.Count == 0)
                {
                    EnhancedUI.DisplayInfo("⚠️ Chưa có món ăn nào trong hệ thống!");
                    Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                               DANH SÁCH MÓN ĂN                                 ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
                Console.WriteLine("║ {0,-8} {1,-25} {2,-15} {3,-12} {4,-10} ║", "Mã", "Tên món", "Nhóm", "Giá", "Tình trạng    ");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

                var pagedDishes = dishList.Skip((page - 1) * pageSize).Take(pageSize);

                foreach (var dish in pagedDishes)
                {
                    string status = dish.IsAvailable ? "✅ Có sẵn" : "❌ Hết hàng";
                    if (!CheckDishIngredients(dish))
                        status = "⚠️ Thiếu NL";

                    Console.WriteLine("║ {0,-8} {1,-25} {2,-15} {3,-12} {4,-10}    ║",
                        dish.Id,
                        TruncateString(dish.Name, 25),
                        TruncateString(dish.Category, 15),
                        $"{dish.Price:N0}đ",
                        status);
                }

                Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
                Console.WriteLine($"\n📄 Trang {page}/{totalPages} | Tổng: {dishList.Count} món");

                Console.WriteLine("\n[ N ] → Trang sau   |   [ P ] → Trang trước");
                Console.WriteLine("[ Số trang ] → Nhảy đến trang bất kỳ");
                Console.WriteLine("[ 0 ] → Thoát xem danh sách");
                Console.Write("\n👉 Chọn: ");
                string choice = Console.ReadLine()?.Trim().ToLower();

                if (choice == "0") break;
                else if (choice == "n" && page < totalPages)
                    page++;
                else if (choice == "p" && page > 1)
                    page--;
                else if (int.TryParse(choice, out int targetPage))
                {
                    if (targetPage >= 1 && targetPage <= totalPages)
                        page = targetPage;
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"⚠️ Trang không hợp lệ! Chỉ từ 1 → {totalPages}");
                        Console.ResetColor();
                        Thread.Sleep(1000);
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("⚠️ Lựa chọn không hợp lệ!");
                    Console.ResetColor();
                    Thread.Sleep(1000);
                }

                Console.Clear();
            }
        }


        private void DisplayIngredients(int page = 1, int pageSize = 20)
        {
            while (true)
            {
                Console.Clear();
                EnhancedUI.DisplayHeader("📦 DANH SÁCH NGUYÊN LIỆU");

                var ingredientList = repository.Ingredients.Values.ToList();
                if (ingredientList.Count == 0)
                {
                    EnhancedUI.DisplayInfo("⚠️ Chưa có nguyên liệu nào trong hệ thống!");
                    Console.WriteLine("\nNhấn phím bất kỳ để quay lại...");
                    Console.ReadKey();
                    return;
                }

                int totalPages = (int)Math.Ceiling(ingredientList.Count / (double)pageSize);
                if (page < 1) page = 1;
                if (page > totalPages) page = totalPages;

                Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                               DANH SÁCH NGUYÊN LIỆU                          ║");
                Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");
                Console.WriteLine("║ {0,-8} {1,-25} {2,-10} {3,-10} {4,-10} {5,-12} ║",
                    "Mã", "Tên", "Đơn vị", "Số lượng", "Tối thiểu", "Trạng thái");
                Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════════╣");

                var pagedIngredients = ingredientList.Skip((page - 1) * pageSize).Take(pageSize);
                foreach (var ing in pagedIngredients)
                {
                    string status = ing.Quantity == 0 ? "❌ Hết hàng"
                                  : ing.IsLowStock ? "⚠️ Sắp hết"
                                  : "✅ Đủ";

                    Console.WriteLine("║ {0,-8} {1,-25} {2,-10} {3,-10} {4,-10} {5,-12} ║",
                        ing.Id,
                        TruncateString(ing.Name, 25),
                        ing.Unit,
                        ing.Quantity,
                        ing.MinQuantity,
                        status);
                }

                Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
                Console.WriteLine($"\nTrang {page}/{totalPages} | Tổng cộng: {ingredientList.Count} nguyên liệu");
                Console.Write("\nNhập số trang muốn xem (0 để thoát): ");

                string input = Console.ReadLine();
                if (int.TryParse(input, out int newPage))
                {
                    if (newPage == 0) break;
                    if (newPage >= 1 && newPage <= totalPages)
                        page = newPage;
                    else
                    {
                        Console.WriteLine("❌ Trang không hợp lệ!");
                        Thread.Sleep(1000);
                    }
                }
                else
                {
                    Console.WriteLine("⚠️ Vui lòng nhập số hợp lệ!");
                    Thread.Sleep(1000);
                }
            }
        }


        // ==================== DISH MANAGEMENT METHODS ====================
        private void UpdateDish()
        {
            int pageSize = 10;
            var dishes = repository.Dishes.Values.Where(d => !string.IsNullOrWhiteSpace(d.Id)).OrderBy(d => d.Id).ToList();
            int totalDishes = dishes.Count;
            int totalPages = (int)Math.Ceiling(totalDishes / (double)pageSize);
            int currentPage = 1;

            if (totalDishes == 0)
            {
                EnhancedUI.DisplayWarning("⚠️  Chưa có món ăn nào trong hệ thống!");
                Console.ReadKey();
                return;
            }

            string dishId = null;

            // Căn giữa text
            string PadCenter(string text, int width)
            {
                if (string.IsNullOrEmpty(text)) text = "";
                if (text.Length >= width) return text.Substring(0, width);
                int leftPadding = (width - text.Length) / 2;
                int rightPadding = width - text.Length - leftPadding;
                return new string(' ', leftPadding) + text + new string(' ', rightPadding);
            }

            // Progress bar
            void DrawProgressBar(int current, int total, int width = 40)
            {
                double percentage = (double)current / total;
                int progressWidth = (int)(width * percentage);
                string progressBar = new string('█', progressWidth) + new string('░', width - progressWidth);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{progressBar}] {current}/{total} ({percentage:P0})");
                Console.ResetColor();
            }

            while (true)
            {
                Console.Clear();
                EnhancedUI.DisplayHeader("🍽️ CẬP NHẬT MÓN ĂN");

                // Tổng quan
                Console.Write("📊 Món có đủ nguyên liệu: ");
                DrawProgressBar(dishes.Count(d => CheckDishIngredients(d)), totalDishes);
                Console.WriteLine($"\n📄 Trang {currentPage}/{totalPages}\n");

                // Header bảng
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"{PadCenter("ID", 8)} │ {PadCenter("Tên món", 30)} │ {PadCenter("Giá", 12)} │ {PadCenter("Trạng thái", 12)}");
                Console.WriteLine(new string('─', 72));
                Console.ResetColor();

                // Danh sách món theo trang
                var dishesToShow = dishes
                    .Skip((currentPage - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                foreach (var dish in dishesToShow)
                {
                    bool ready = CheckDishIngredients(dish);
                    string status = ready ? "✅ Sẵn sàng" : "⚠️ Thiếu NL";

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write(PadCenter(dish.Id, 8) + " │ ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(PadCenter(dish.Name, 30) + " │ ");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write(PadCenter(dish.Price.ToString("N0") + "đ", 12) + " │ ");
                    Console.ForegroundColor = ready ? ConsoleColor.Green : ConsoleColor.Red;
                    Console.WriteLine(PadCenter(status, 12));
                    Console.ResetColor();
                }

                Console.WriteLine(new string('─', 72));
                Console.WriteLine("Nhập số trang để chuyển • Nhập mã món để cập nhật • Nhập 0 để thoát");
                Console.Write("\n👉 Lựa chọn: ");
                string input = Console.ReadLine()?.Trim();

                if (input == "0") return;

                if (int.TryParse(input, out int page))
                {
                    if (page >= 1 && page <= totalPages)
                    {
                        currentPage = page;
                        continue;
                    }
                    EnhancedUI.DisplayError("⚠️  Số trang không hợp lệ!");
                    Console.ReadKey();
                    continue;
                }

                // Tìm món theo ID
                if (!repository.Dishes.ContainsKey(input))
                {
                    EnhancedUI.DisplayError("❌ Mã món không tồn tại!");
                    Console.ReadKey();
                    continue;
                }

                dishId = input;
                var oldDish = repository.Dishes[dishId];
                var newDish = new Dish(oldDish.Id, oldDish.Name, oldDish.Description, oldDish.Price, oldDish.Category)
                {
                    IsAvailable = oldDish.IsAvailable,
                    Ingredients = new Dictionary<string, decimal>(oldDish.Ingredients),
                    SalesCount = oldDish.SalesCount,
                    Cost = oldDish.Cost
                };

                // === Cập nhật chi tiết ===
                string[] steps =
                {
            "Tên món", "Mô tả", "Giá", "Nhóm món", "Trạng thái", "Nguyên liệu", "Hoàn tất"
        };

                Console.Clear();
                EnhancedUI.DisplayHeader($"🔧 CẬP NHẬT MÓN: {oldDish.Name}");

                for (int i = 0; i < steps.Length; i++)
                {
                    Console.Write($"⏳ {steps[i]}... ");
                    DrawProgressBar(i + 1, steps.Length, 30);
                    Console.WriteLine();

                    switch (i)
                    {
                        case 0:
                            Console.Write($"   Tên món ({oldDish.Name}): ");
                            string name = Console.ReadLine();
                            if (!string.IsNullOrEmpty(name)) newDish.Name = name;
                            break;

                        case 1:
                            Console.Write($"   Mô tả ({oldDish.Description}): ");
                            string desc = Console.ReadLine();
                            if (!string.IsNullOrEmpty(desc)) newDish.Description = desc;
                            break;

                        case 2:
                            Console.Write($"   Giá ({oldDish.Price:N0}đ): ");
                            string pStr = Console.ReadLine();
                            if (!string.IsNullOrEmpty(pStr) && decimal.TryParse(pStr, out decimal price))
                                newDish.Price = price;
                            break;

                        case 3:
                            Console.WriteLine($"   Nhóm món hiện tại: {oldDish.Category}");
                            if (EnhancedUI.Confirm("   Đổi nhóm món?"))
                            {
                                string cat = SelectCategory();
                                if (!string.IsNullOrEmpty(cat)) newDish.Category = cat;
                            }
                            break;

                        case 4:
                            Console.Write($"   Trạng thái (1-Có sẵn, 0-Hết hàng) [{(oldDish.IsAvailable ? "1" : "0")}]: ");
                            string status = Console.ReadLine();
                            if (!string.IsNullOrEmpty(status)) newDish.IsAvailable = status == "1";
                            break;

                        case 5:
                            if (EnhancedUI.Confirm("   Cập nhật nguyên liệu?"))
                                ManageDishIngredients(newDish);
                            break;

                        case 6:
                            undoRedoService.ExecuteCommand(new UpdateDishCommand(this, oldDish, newDish));
                            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "UPDATE_DISH", "DISH", dishId, $"Cập nhật món: {newDish.Name}"));
                            SaveAllData();
                            break;
                    }

                    Thread.Sleep(150);
                }

                EnhancedUI.DisplaySuccess("🎉 Cập nhật thành công!");
                Console.WriteLine($"\n📋 {newDish.Id} - {newDish.Name} ({newDish.Price:N0}đ) ✅");

                Console.WriteLine("\nNhấn phím bất kỳ để quay lại danh sách...");
                Console.ReadKey();
            }
        }



        private void DeleteDish()
        {
            int pageSize = 10;
            int currentPage = 1;

            string PadCenter(string text, int width)
            {
                text = text ?? "";
                if (text.Length >= width) return text.Substring(0, width);
                int left = (width - text.Length) / 2;
                int right = width - text.Length - left;
                return new string(' ', left) + text + new string(' ', right);
            }

            void AnimateDelete(string dishName)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Write($"\n🗑️ Đang xóa {dishName} ");
                for (int i = 0; i <= 10; i++)
                {
                    Console.Write("█");
                    Thread.Sleep(40);
                }
                Console.WriteLine(" ✅");
                Console.ResetColor();
            }

            ConsoleColor headerColor = ConsoleColor.Cyan;
            ConsoleColor idColor = ConsoleColor.Yellow;
            ConsoleColor nameColor = ConsoleColor.White;
            ConsoleColor priceColor = ConsoleColor.Green;
            ConsoleColor statusColor = ConsoleColor.Magenta;
            ConsoleColor inputColor = ConsoleColor.DarkCyan;

            while (true)
            {
                Console.Clear();
                EnhancedUI.DisplayHeader("🗑️ XÓA MÓN ĂN");

                // Tính lại tổng động mỗi lần vào vòng lặp (tránh sai sau khi xóa)
                int totalDishes = repository.Dishes.Count;
                int totalPages = Math.Max(1, (int)Math.Ceiling(totalDishes / (double)pageSize));
                if (currentPage > totalPages) currentPage = totalPages;

                Console.ForegroundColor = headerColor;
                Console.WriteLine($"Trang {currentPage}/{totalPages}\n");
                Console.ResetColor();

                // Bảng
                Console.ForegroundColor = headerColor;
                Console.WriteLine($"│ {PadCenter("ID", 8)} │ {PadCenter("Tên món", 30)} │ {PadCenter("Giá", 14)} │ {PadCenter("Trạng thái", 12)} │");
                Console.WriteLine("├" + new string('─', 8) + "┼" + new string('─', 30) + "┼" + new string('─', 14) + "┼" + new string('─', 12) + "┤");
                Console.ResetColor();

                var pageItems = repository.Dishes.Values
                    .OrderBy(d => d.Id)
                    .Skip((currentPage - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                foreach (var dish in pageItems)
                {
                    bool ingredientsReady = CheckDishIngredients(dish);
                    string statusIcon = ingredientsReady ? "✅" : "⚠️";

                    Console.ForegroundColor = idColor;
                    Console.Write($"│ {PadCenter(dish.Id, 8)} │ ");
                    Console.ForegroundColor = nameColor;
                    Console.Write($"{PadCenter(dish.Name, 30)} │ ");
                    Console.ForegroundColor = priceColor;
                    Console.Write($"{PadCenter(dish.Price.ToString("N0") + "đ", 14)} │ ");
                    Console.ForegroundColor = statusColor;
                    Console.WriteLine($"{PadCenter(statusIcon + (ingredientsReady ? " Sẵn" : " Thiếu"), 12)} │");
                    Console.ResetColor();
                }

                Console.WriteLine("\nNhập số trang để chuyển, nhập mã món (hoặc nhiều mã cách nhau bởi dấu phẩy) để xóa, hoặc 0 để thoát:");
                Console.ForegroundColor = inputColor;
                Console.Write("👉 Nhập lựa chọn: ");
                Console.ResetColor();

                string input = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(input)) continue;
                if (input == "0") return;

                // chuyển trang nếu người dùng nhập số
                if (int.TryParse(input, out int page))
                {
                    if (page >= 1 && page <= totalPages)
                    {
                        currentPage = page;
                        continue;
                    }
                    EnhancedUI.DisplayError("Số trang không hợp lệ!");
                    Console.ReadKey();
                    continue;
                }

                // tách input thành các mã món, an toàn với phiên bản .NET/C# cũ
                var dishIds = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(x => x.Trim())
                                   .Where(x => !string.IsNullOrEmpty(x))
                                   .ToList();

                if (!dishIds.Any())
                {
                    EnhancedUI.DisplayError("Không có mã món hợp lệ!");
                    Console.ReadKey();
                    continue;
                }

                // Xử lý từng mã một — không thay đổi collection đang lặp trên pageItems
                foreach (var dishId in dishIds)
                {
                    // kiểm tra tồn tại an toàn
                    if (!repository.Dishes.TryGetValue(dishId, out var dish))
                    {
                        EnhancedUI.DisplayError($"Món '{dishId}' không tồn tại!");
                        continue;
                    }

                    // kiểm tra sử dụng trong combo (cẩn trọng nếu Combo.DishIds có thể null)
                    bool isUsedInCombo = repository.Combos.Values.Any(c => c.DishIds != null && c.DishIds.Contains(dishId));
                    if (isUsedInCombo)
                    {
                        EnhancedUI.DisplayError($"Không thể xóa '{dish.Name}' vì đang được dùng trong combo!");
                        continue;
                    }

                    // hiển thị thông tin tóm tắt
                    Console.ForegroundColor = headerColor;
                    Console.WriteLine($"\nThông tin món: {dish.Name} ({dish.Id})");
                    Console.ResetColor();
                    Console.WriteLine($"- Giá: {dish.Price:N0}đ");
                    Console.WriteLine($"- Đã bán: {dish.SalesCount} lượt");
                    int ingredientCount = (dish.Ingredients != null) ? dish.Ingredients.Count : 0;
                    Console.WriteLine($"- Số nguyên liệu: {ingredientCount}");

                    if (!EnhancedUI.Confirm($"Xác nhận xóa món '{dish.Name}'?"))
                        continue;

                    try
                    {
                        AnimateDelete(dish.Name);

                        // Sử dụng command pattern (DeleteDishCommand) để đảm bảo undo/redo (giả định command đã làm xóa trong repository)
                        var command = new DeleteDishCommand(this, dish);
                        undoRedoService.ExecuteCommand(command);

                        repository.AuditLogs.Add(new AuditLog(currentUser.Username, "DELETE_DISH", "DISH", dishId, $"Xóa món: {dish.Name}"));
                        SaveAllData();

                        EnhancedUI.DisplaySuccess($"Đã xóa '{dish.Name}' thành công!");
                        Logger.Info($"Dish {dishId} deleted", "DishManagement");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to delete dish {dishId}", "DishManagement", ex);
                        EnhancedUI.DisplayError($"Lỗi khi xóa '{dish.Name}': {ex.Message}");
                    }
                }

                // sau khi xóa, cập nhật lại tổng số trang / trang hiện tại
                totalDishes = repository.Dishes.Count;
                totalPages = Math.Max(1, (int)Math.Ceiling(totalDishes / (double)pageSize));
                if (currentPage > totalPages) currentPage = totalPages;

                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
            }
        }

        private void SearchDishes()
        {
            EnhancedUI.DisplayHeader("🔍 TÌM KIẾM MÓN ĂN");

            Console.Write("Nhập từ khóa tìm kiếm: ");
            string keyword = Console.ReadLine()?.Trim()?.ToLower();

            if (string.IsNullOrEmpty(keyword))
            {
                EnhancedUI.DisplayError("⚠️  Vui lòng nhập từ khóa tìm kiếm!");
                Console.ReadKey();
                return;
            }

            var results = repository.Dishes.Values
                .Where(d =>
                    (d.Name ?? "").ToLower().Contains(keyword) ||
                    (d.Description ?? "").ToLower().Contains(keyword) ||
                    (d.Category ?? "").ToLower().Contains(keyword) ||
                    (d.Id ?? "").ToLower().Contains(keyword))
                .OrderBy(d => d.Id)
                .ToList();

            if (!results.Any())
            {
                EnhancedUI.DisplayError($"❌ Không tìm thấy món nào với từ khóa '{keyword}'.");
                Console.ReadKey();
                return;
            }

            int pageSize = 10;
            int totalPages = Math.Max(1, (int)Math.Ceiling(results.Count / (double)pageSize));
            int currentPage = 1;

            while (true)
            {
                Console.Clear();
                EnhancedUI.DisplayHeader($"📖 KẾT QUẢ TÌM KIẾM — '{keyword}'");
                Console.WriteLine($"Trang {currentPage}/{totalPages} | Tổng {results.Count} món\n");

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("╔═════════════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║ {0,-8} │ {1,-25} │ {2,-15} │ {3,-12} ║", "Mã", "Tên món", "Nhóm", "Giá (VNĐ)         ");
                Console.WriteLine("╠═════════════════════════════════════════════════════════════════════════════╣");
                Console.ResetColor();

                var pageItems = results
                    .Skip((currentPage - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                foreach (var dish in pageItems)
                {
                    Console.WriteLine("║ {0,-8} │ {1,-25} │ {2,-15} │ {3,-12}       ║",
                        dish.Id,
                        TruncateString(dish.Name, 25),
                        TruncateString(dish.Category, 15),
                        $"{dish.Price:N0}đ");
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("╚═════════════════════════════════════════════════════════════════════════════╝");
                Console.ResetColor();

                Console.WriteLine("\n👉 Nhập:");
                Console.WriteLine(" - Số trang để chuyển (VD: 2)");
                Console.WriteLine(" - Mã món để xem chi tiết");
                Console.WriteLine(" - 0 để quay lại menu chính");

                Console.Write("\nLựa chọn: ");
                string input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input)) continue;
                if (input == "0") break;

                // Chuyển trang
                if (int.TryParse(input, out int page))
                {
                    if (page >= 1 && page <= totalPages)
                    {
                        currentPage = page;
                        continue;
                    }
                    EnhancedUI.DisplayError("❌ Số trang không hợp lệ!");
                    Console.ReadKey();
                    continue;
                }

                // Xem chi tiết món theo mã
                var selected = results.FirstOrDefault(d => d.Id.Equals(input, StringComparison.OrdinalIgnoreCase));
                if (selected != null)
                {
                    Console.Clear();
                    EnhancedUI.DisplayHeader($"🍽️ THÔNG TIN CHI TIẾT — {selected.Name}");
                    Console.WriteLine($"🔹 Mã món: {selected.Id}");
                    Console.WriteLine($"🔹 Tên món: {selected.Name}");
                    Console.WriteLine($"🔹 Nhóm: {selected.Category}");
                    Console.WriteLine($"🔹 Giá bán: {selected.Price:N0}đ");
                    Console.WriteLine($"🔹 Mô tả: {selected.Description}");
                    Console.WriteLine($"🔹 Nguyên liệu: {(selected.Ingredients != null ? selected.Ingredients.Count : 0)} loại");
                    Console.WriteLine("\nNhấn phím bất kỳ để quay lại danh sách...");
                    Console.ReadKey();
                }
                else
                {
                    EnhancedUI.DisplayError("⚠️ Không tìm thấy mã món này trong kết quả tìm kiếm!");
                    Console.ReadKey();
                }
            }

            Logger.Info($"Searched dishes with keyword: {keyword} - Found {results.Count} results", "DishManagement");
        }


        private void FilterDishes()
        {
            while (true)
            {
                EnhancedUI.DisplayHeader("LỌC MÓN ĂN");

                Console.WriteLine("1. Theo giá (thấp → cao)");
                Console.WriteLine("2. Theo giá (cao → thấp)");
                Console.WriteLine("3. Theo nhóm món");
                Console.WriteLine("4. Món còn nguyên liệu");
                Console.WriteLine("5. Món hết nguyên liệu");
                Console.WriteLine("6. Món bán chạy nhất");
                Console.WriteLine("7. Món lợi nhuận cao");
                Console.WriteLine("0. Thoát");
                Console.Write("\nChọn tiêu chí lọc: ");

                string choice = Console.ReadLine();
                if (choice == "0") break;

                var dishes = repository.Dishes.Values.ToList();
                List<Dish> results = new List<Dish>();

                switch (choice)
                {
                    case "1":
                        results = dishes.OrderBy(d => d.Price).ToList();
                        break;
                    case "2":
                        results = dishes.OrderByDescending(d => d.Price).ToList();
                        break;
                    case "3":
                        Console.WriteLine("\n--- Danh sách nhóm món có sẵn ---");
                        var categories = dishes.Select(d => d.Category)
                                               .Distinct()
                                               .OrderBy(c => c)
                                               .ToList();

                        for (int i = 0; i < categories.Count; i++)
                            Console.WriteLine($"{i + 1}. {categories[i]}");

                        Console.Write("\nChọn số tương ứng nhóm món: ");
                        if (int.TryParse(Console.ReadLine(), out int groupChoice) && groupChoice > 0 && groupChoice <= categories.Count)
                        {
                            string selectedCategory = categories[groupChoice - 1];
                            results = dishes.Where(d => d.Category.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase)).ToList();
                        }
                        else
                        {
                            Console.WriteLine("❌ Lựa chọn không hợp lệ, hiển thị tất cả món.");
                            results = dishes;
                        }
                        break;

                    case "4":
                        results = dishes.Where(d => CheckDishIngredients(d)).ToList();
                        break;
                    case "5":
                        results = dishes.Where(d => !CheckDishIngredients(d)).ToList();
                        break;
                    case "6":
                        results = dishes.OrderByDescending(d => d.SalesCount).ToList();
                        break;
                    case "7":
                        results = dishes.Where(d => d.Cost > 0)
                                        .OrderByDescending(d => d.ProfitMargin)
                                        .ToList();
                        break;
                    default:
                        EnhancedUI.DisplayError("❌ Lựa chọn không hợp lệ!");
                        Console.ReadKey();
                        continue;
                }

                if (!results.Any())
                {
                    EnhancedUI.DisplayError("❌ Không tìm thấy món ăn nào theo tiêu chí lọc!");
                    Console.ReadKey();
                    continue;
                }

                int pageSize = 10;
                int totalPages = (int)Math.Ceiling(results.Count / (double)pageSize);
                int currentPage = 1;

                while (true)
                {
                    Console.Clear();
                    Console.WriteLine("═════════════════════════════════════════════════════════════════════════════════════════════════");
                    Console.WriteLine("                                    KẾT QUẢ LỌC MÓN ĂN                                            ");
                    Console.WriteLine("═════════════════════════════════════════════════════════════════════════════════════════════════");
                    Console.WriteLine($"{"MÃ",-10} {"TÊN MÓN",-27} {"NHÓM",-17} {"   GIÁ",-16} {"LỢI NHUẬN %",-12} {"TÌNH TRẠNG",-14}");
                    Console.WriteLine("═════════════════════════════════════════════════════════════════════════════════════════════════");

                    var pageItems = results.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();
                    foreach (var d in pageItems)
                    {
                        string profit = d.Cost > 0 ? $"{d.ProfitMargin:F1}%" : "N/A";
                        string status = CheckDishIngredients(d) ? "Có NL" : "Thiếu NL";
                        Console.WriteLine($"{d.Id,-10} {TruncateString(d.Name, 25),-25} {TruncateString(d.Category, 15),-15} {d.Price,12:N0} {profit,14} {status,14}");
                    }

                    Console.WriteLine("═════════════════════════════════════════════════════════════════════════════════════════════════");
                    Console.WriteLine($"Trang {currentPage}/{totalPages} | Tổng: {results.Count} món");
                    Console.WriteLine("\nChọn: [S] Xuất CSV | [<] Trang trước | [>] Trang sau | [Nhập số trang] | [Q] Quay lại menu lọc");
                    Console.Write("➜ ");

                    string input = Console.ReadLine().Trim().ToLower();

                    if (input == "q") break;
                    else if (input == "s")
                    {
                        string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                        string filePath = Path.Combine(downloadPath, "Dishes_Filtered.csv");

                        using (var writer = new StreamWriter(filePath))
                        {
                            writer.WriteLine("ID,Tên món,Nhóm,Giá,Lợi nhuận %,Tình trạng");
                            foreach (var dish in results)
                            {
                                string profitText = dish.Cost > 0 ? $"{dish.ProfitMargin:F1}%" : "N/A";
                                string status = CheckDishIngredients(dish) ? "Có NL" : "Thiếu NL";
                                writer.WriteLine($"{dish.Id},{dish.Name},{dish.Category},{dish.Price:N0},{profitText},{status}");
                            }
                        }

                        Console.WriteLine($"\n✅ Đã xuất kết quả ra: {filePath}");
                        Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
                        Console.ReadKey();
                    }
                    else if (input == "<" && currentPage > 1)
                    {
                        currentPage--;
                    }
                    else if (input == ">" && currentPage < totalPages)
                    {
                        currentPage++;
                    }
                    else if (int.TryParse(input, out int pageNum))
                    {
                        if (pageNum >= 1 && pageNum <= totalPages)
                            currentPage = pageNum;
                        else
                        {
                            EnhancedUI.DisplayError("❌ Số trang không hợp lệ!");
                            Thread.Sleep(800);
                        }
                    }
                }
            }
        }



        private void ShowDishDetail()
        {
            int pageSize = 10;
            var dishes = repository.Dishes.Values.ToList();
            int totalPages = (int)Math.Ceiling(dishes.Count / (double)pageSize);
            int currentPage = 1;

            while (true)
            {
                Console.Clear();
                EnhancedUI.DisplayHeader("📖 DANH SÁCH MÓN ĂN");

                Console.WriteLine("╔════════╦══════════════════════════════════════╦══════════════╦══════════════╗");
                Console.WriteLine("║  MÃ MÓN ║ TÊN MÓN                              ║    GIÁ (VNĐ) ║  TRẠNG THÁI  ║");
                Console.WriteLine("╠════════╬══════════════════════════════════════╬══════════════╬══════════════╣");

                var pageItems = dishes
                    .Skip((currentPage - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                foreach (var dish1 in pageItems)
                {
                    Console.WriteLine($"║ {dish1.Id,-6} ║ {TruncateString(dish1.Name, 36),-36} ║ {dish1.Price,10:N0} ║ {(dish1.IsAvailable ? "✅ Có sẵn " : "❌ Hết hàng"),-10} ║");
                }

                Console.WriteLine("╚════════╩══════════════════════════════════════╩══════════════╩══════════════╝");
                Console.WriteLine($"Trang {currentPage}/{totalPages}");
                Console.WriteLine("➡ Nhập số trang để chuyển, hoặc nhập trực tiếp mã món để xem chi tiết | '0' để thoát");

                Console.Write("\n👉 Nhập lựa chọn: ");
                string input = Console.ReadLine()?.Trim();

                if (input == "0")
                    return;

                // Chuyển trang nếu nhập số
                if (int.TryParse(input, out int newPage))
                {
                    if (newPage >= 1 && newPage <= totalPages)
                        currentPage = newPage;
                    else
                        EnhancedUI.DisplayError("❌ Số trang không hợp lệ!");
                    continue;
                }

                // Nếu nhập mã món
                if (!repository.Dishes.ContainsKey(input))
                {
                    EnhancedUI.DisplayError("❌ Món ăn không tồn tại!");
                    Console.ReadKey();
                    continue;
                }

                var dish = repository.Dishes[input];
                dish.CalculateCost(repository.Ingredients);

                Console.Clear();
                EnhancedUI.DisplayHeader($"📘 CHI TIẾT MÓN: {dish.Name}");

                // Hiển thị dạng Tree
                Console.WriteLine($"{dish.Name} ({dish.Id})");
                Console.WriteLine($"├─ Mô tả: {TruncateString(dish.Description, 50)}");
                Console.WriteLine($"├─ Giá bán: {dish.Price:N0}đ");
                Console.WriteLine($"├─ Nhóm: {dish.Category}");
                Console.WriteLine($"├─ Tình trạng: {(dish.IsAvailable ? "✅ Có sẵn" : "❌ Hết hàng")}");
                Console.WriteLine($"├─ Số lượt bán: {dish.SalesCount}");
                Console.WriteLine($"├─ Chi phí NL: {dish.Cost:N0}đ");
                Console.WriteLine($"├─ Lợi nhuận: {dish.ProfitMargin:F1}%");
                Console.WriteLine($"└─ Tình trạng NL: {(CheckDishIngredients(dish) ? "✅ Đủ" : "❌ Thiếu")}");

                // Nguyên liệu dạng Tree
                Console.WriteLine("\nNguyên liệu:");
                if (dish.Ingredients.Any())
                {
                    int idx = 1;
                    decimal totalCost = 0;
                    foreach (var ing in dish.Ingredients)
                    {
                        if (repository.Ingredients.ContainsKey(ing.Key))
                        {
                            var ingredient = repository.Ingredients[ing.Key];
                            decimal cost = ingredient.PricePerUnit * ing.Value;
                            totalCost += cost;
                            string status = ingredient.Quantity >= ing.Value ? "✅" : "❌";

                            Console.WriteLine($"   ├─ {idx++}. {ingredient.Name}: {ing.Value} {ingredient.Unit} | {cost:N0}đ {status}");
                        }
                    }
                    Console.WriteLine($"   └─ Tổng chi phí: {totalCost:N0}đ");
                }
                else
                {
                    Console.WriteLine("   └─ Chưa có nguyên liệu.");
                }

                Console.WriteLine("\nNhấn phím bất kỳ để quay lại danh sách...");
                Console.ReadKey();
            }
        }



        private void BatchUpdateDishes()
        {
            EnhancedUI.DisplayHeader("CẬP NHẬT MÓN ĂN HÀNG LOẠT");

            var menuOptions = new List<string>
    {
        "📊 Cập nhật giá theo phần trăm",
        "🔄 Cập nhật trạng thái sẵn có",
        "📁 Cập nhật nhóm món ăn",
        "💰 Cập nhật giá cố định",
        "🏷 Áp dụng khuyến mãi theo giá "
    };

            while (true)
            {
                int choice = EnhancedUI.ShowMenu("CẬP NHẬT HÀNG LOẠT", menuOptions);
                if (choice == 0) return;

                try
                {
                    switch (choice)
                    {
                        case 1: BatchUpdatePricesByPercent(); break;
                        case 2: BatchUpdateAvailability(); break;
                        case 3: BatchUpdateCategories(); break;
                        case 4: BatchUpdateFixedPrices(); break;
                        case 5: BatchApplyDiscountByPrice(); break;
                    }

                    // Sau mỗi thao tác, hỏi người dùng có muốn tiếp tục không
                    if (!EnhancedUI.Confirm("Tiếp tục cập nhật hàng loạt?"))
                        return;
                }
                catch (Exception ex)
                {
                    Logger.Error("Batch update dishes failed", "DishManagement", ex);
                    EnhancedUI.DisplayError($"Lỗi: {ex.Message}");
                    Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                    Console.ReadKey();
                }
            }
        }

        // ==================== HELPER METHODS ====================

        private string SelectUpdateScope()
        {
            Console.WriteLine("\n🎯 PHẠM VI ÁP DỤNG:");
            Console.WriteLine("1. 📂 Theo nhóm món");
            Console.WriteLine("2. 💰 Theo khoảng giá");
            Console.WriteLine("3. 📊 Món có lợi nhuận thấp");
            Console.WriteLine("4. 🔥 Món bán chạy");
            Console.WriteLine("5. 🌟 Tất cả món ăn");
            Console.Write("Lựa chọn: ");

            string scopeChoice = Console.ReadLine();
            switch (scopeChoice)
            {
                case "1":
                    Console.Write("Nhập tên nhóm món: ");
                    return Console.ReadLine()?.Trim();
                case "2":
                    Console.Write("Giá tối thiểu (để trống nếu không giới hạn): ");
                    decimal min;
                    decimal? minPrice = null;
                    string input = Console.ReadLine();

                    if (decimal.TryParse(input, out min))
                    {
                        minPrice = min;
                    }
                    Console.Write("Giá tối đa (để trống nếu không giới hạn): ");
                    decimal max;
                    decimal? maxPrice = null;
                    string input2 = Console.ReadLine();

                    if (decimal.TryParse(input2, out max))
                    {
                        maxPrice = max;
                    }
                    return $"price:{minPrice}:{maxPrice}";
                case "3":
                    return "low_profit";
                case "4":
                    Console.Write("Số lượt bán tối thiểu: ");
                    int minSales = int.TryParse(Console.ReadLine(), out int sales) ? sales : 10;
                    return $"popular:{minSales}";
                case "5":
                    return "all";
                default:
                    EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                    return null;
            }
        }

        private (List<Dish> dishes, string filterInfo) GetDishesByScope(string scope)
        {
            if (scope == "all")
            {
                return (repository.Dishes.Values.ToList(), "");
            }
            else if (scope == "low_profit")
            {
                var lowProfitDishes = repository.Dishes.Values
                    .Where(d => d.Cost > 0 && d.ProfitMargin < 20)
                    .ToList();
                return (lowProfitDishes, " (lợi nhuận thấp)");
            }
            else if (scope.StartsWith("price:"))
            {
                var parts = scope.Split(':');
                decimal? minPrice = null, maxPrice = null;

                if (!string.IsNullOrWhiteSpace(parts[1])) minPrice = Convert.ToDecimal(parts[1]);
                if (!string.IsNullOrWhiteSpace(parts[2])) maxPrice = Convert.ToDecimal(parts[2]);

                var priceDishes = repository.Dishes.Values.Where(d =>
                    (!minPrice.HasValue || d.Price >= minPrice.Value) &&
                    (!maxPrice.HasValue || d.Price <= maxPrice.Value)).ToList();

                string rangeInfo = $"";
                if (minPrice.HasValue && maxPrice.HasValue)
                    rangeInfo = $" (giá từ {minPrice.Value:N0}đ đến {maxPrice.Value:N0}đ)";
                else if (minPrice.HasValue)
                    rangeInfo = $" (giá từ {minPrice.Value:N0}đ)";
                else if (maxPrice.HasValue)
                    rangeInfo = $" (giá đến {maxPrice.Value:N0}đ)";

                return (priceDishes, rangeInfo);
            }
            else if (scope.StartsWith("popular:"))
            {
                int minSales = int.Parse(scope.Split(':')[1]);
                var popularDishes = repository.Dishes.Values
                    .Where(d => d.SalesCount >= minSales)
                    .ToList();
                return (popularDishes, $" (bán chạy từ {minSales} lượt)");
            }
            else
            {
                // Theo nhóm món
                var categoryDishes = repository.Dishes.Values
                    .Where(d => d.Category.Equals(scope, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                return (categoryDishes, $" (nhóm '{scope}')");
            }
        }

        private void BatchUpdateFixedPrices()
        {
            EnhancedUI.DisplayHeader("💰 CẬP NHẬT GIÁ CỐ ĐỊNH");

            DisplayPriceStatistics();

            Console.WriteLine("\n🎯 THIẾT LẬP CẬP NHẬT:");

            string scope = SelectUpdateScope();
            if (scope == null) return;

            Console.Write("\n💵 Nhập giá mới (VNĐ): ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal newPrice) || newPrice < 0)
            {
                EnhancedUI.DisplayError("Giá không hợp lệ!");
                return;
            }

            var (dishesToUpdate, filterInfo) = GetDishesByScope(scope);
            if (!dishesToUpdate.Any())
            {
                EnhancedUI.DisplayWarning("Không tìm thấy món ăn nào phù hợp!");
                return;
            }

            // Hiển thị xem trước thay đổi
            Console.WriteLine($"\n📊 THAY ĐỔI GIÁ SẼ ÁP DỤNG:");
            Console.WriteLine($"   Giá mới: {newPrice:N0}đ");
            Console.WriteLine($"   Số món: {dishesToUpdate.Count}");

            decimal totalChange = dishesToUpdate.Sum(d => newPrice - d.Price);
            Console.WriteLine($"   Tổng thay đổi doanh thu: {totalChange:N0}đ");

            if (ConfirmBatchUpdate($"đặt giá cố định {newPrice:N0}đ cho {dishesToUpdate.Count} món ăn{filterInfo}"))
            {
                var oldDishesState = dishesToUpdate.Select(d => new Dish(d.Id, d.Name, d.Description, d.Price, d.Category)
                {
                    IsAvailable = d.IsAvailable,
                    Ingredients = new Dictionary<string, decimal>(d.Ingredients),
                    SalesCount = d.SalesCount,
                    Cost = d.Cost
                }).ToList();

                foreach (var dish in dishesToUpdate)
                {
                    dish.Price = newPrice;
                }

                var command = new BatchUpdateDishesCommand(this, oldDishesState, dishesToUpdate);
                undoRedoService.ExecuteCommand(command);

                repository.AuditLogs.Add(new AuditLog(currentUser.Username, "BATCH_UPDATE_PRICES", "DISH", "",
                    $"Đặt giá {newPrice:N0}đ cho {dishesToUpdate.Count} món{filterInfo}"));
                SaveAllData();

                EnhancedUI.DisplaySuccess($"✅ Đã cập nhật giá thành công cho {dishesToUpdate.Count} món ăn!");
            }
        }
        private void BatchUpdateCategories()
        {
            EnhancedUI.DisplayHeader("📁 CẬP NHẬT NHÓM MÓN ĂN");

            // Hiển thị danh sách nhóm món hiện có
            DisplayCurrentCategories();

            Console.WriteLine("\n🎯 THIẾT LẬP CẬP NHẬT:");

            // Chọn nhóm món nguồn
            Console.Write("Nhóm món cần thay đổi (để trống nếu áp dụng cho tất cả): ");
            string oldCategory = Console.ReadLine()?.Trim();

            // Chọn nhóm món đích
            string newCategory = SelectCategory();
            if (string.IsNullOrEmpty(newCategory)) return;

            // Lấy danh sách món cần cập nhật
            var dishesToUpdate = string.IsNullOrEmpty(oldCategory)
                ? repository.Dishes.Values.ToList()
                : repository.Dishes.Values.Where(d => d.Category.Equals(oldCategory, StringComparison.OrdinalIgnoreCase)).ToList();

            string filterInfo = string.IsNullOrEmpty(oldCategory)
                ? "" : $" từ nhóm '{oldCategory}'";

            if (!dishesToUpdate.Any())
            {
                EnhancedUI.DisplayWarning("Không tìm thấy món ăn nào phù hợp!");
                return;
            }

            // Hiển thị xem trước thay đổi
            Console.WriteLine($"\n📋 MÓN SẼ ĐƯỢC CHUYỂN TỪ '{oldCategory ?? "Tất cả"}' SANG '{newCategory}':");
            foreach (var dish in dishesToUpdate.Take(5))
            {
                Console.WriteLine($"   - {dish.Name} ({dish.Price:N0}đ)");
            }
            if (dishesToUpdate.Count > 5)
            {
                Console.WriteLine($"   ... và {dishesToUpdate.Count - 5} món khác");
            }

            // Xác nhận thực hiện
            if (ConfirmBatchUpdate($"chuyển {dishesToUpdate.Count} món{filterInfo} sang nhóm '{newCategory}'"))
            {
                ExecuteBatchCategoryUpdate(dishesToUpdate, newCategory,
                    $"Chuyển nhóm '{oldCategory}' sang '{newCategory}'");

                EnhancedUI.DisplaySuccess($"✅ Đã chuyển nhóm thành công cho {dishesToUpdate.Count} món ăn!");
            }
        }

        private void DisplayPriceStatistics()
        {
            var dishes = repository.Dishes.Values.ToList();
            if (!dishes.Any()) return;

            decimal avgPrice = dishes.Average(d => d.Price);
            decimal maxPrice = dishes.Max(d => d.Price);
            decimal minPrice = dishes.Min(d => d.Price);

            Console.WriteLine("📊 THỐNG KÊ GIÁ HIỆN TẠI:");
            Console.WriteLine($"   • Số món: {dishes.Count}");
            Console.WriteLine($"   • Giá trung bình: {avgPrice:N0}đ");
            Console.WriteLine($"   • Giá cao nhất: {maxPrice:N0}đ");
            Console.WriteLine($"   • Giá thấp nhất: {minPrice:N0}đ");
        }
        private void DisplayAvailabilityStatistics()
        {
            var availableCount = repository.Dishes.Values.Count(d => d.IsAvailable);
            var unavailableCount = repository.Dishes.Values.Count(d => !d.IsAvailable);

            Console.WriteLine("📊 THỐNG KÊ TRẠNG THÁI HIỆN TẠI:");
            Console.WriteLine($"   • ✅ Có sẵn: {availableCount} món");
            Console.WriteLine($"   • ❌ Tạm hết: {unavailableCount} món");
        }
        private void DisplayCurrentCategories()
        {
            var categories = repository.Dishes.Values
                .GroupBy(d => d.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            Console.WriteLine("📁 DANH SÁCH NHÓM MÓN HIỆN CÓ:");
            foreach (var cat in categories.Take(10))
            {
                Console.WriteLine($"   • {cat.Category}: {cat.Count} món");
            }
            if (categories.Count > 10)
            {
                Console.WriteLine($"   ... và {categories.Count - 10} nhóm khác");
            }
        }

        private void DisplayPriceChangePreview(List<Dish> dishes, decimal percent)
        {
            Console.WriteLine($"\n📊 XEM TRƯỚC THAY ĐỔI ({percent}%):");

            // Tính toán trước các giá trị
            decimal totalCurrentRevenue = 0;
            decimal totalNewRevenue = 0;

            foreach (var dish in dishes.Take(3))
            {
                decimal newPrice = Math.Round(dish.Price * (1 + percent / 100), 0);
                decimal change = newPrice - dish.Price;

                // Ước tính doanh thu dựa trên số lượt bán trung bình
                decimal estimatedMonthlySales = dish.SalesCount > 0 ? dish.SalesCount / 3.0m : 10; // Nếu không có data, ước tính 10 lượt/tháng
                decimal monthlyRevenueChange = change * estimatedMonthlySales;

                totalCurrentRevenue += dish.Price * estimatedMonthlySales;
                totalNewRevenue += newPrice * estimatedMonthlySales;

                Console.WriteLine($"   - {dish.Name}: {dish.Price:N0}đ → {newPrice:N0}đ ({change:+#;-#;0}đ)");
                if (dish.SalesCount == 0)
                {
                    Console.WriteLine($"     📈 (Ước tính: {monthlyRevenueChange:+#;-#;0}đ/tháng)");
                }
            }

            if (dishes.Count > 3)
            {
                // Tính toán cho tất cả các món
                foreach (var dish in dishes.Skip(3))
                {
                    decimal estimatedMonthlySales = dish.SalesCount > 0 ? dish.SalesCount / 3.0m : 10;
                    decimal newPrice = Math.Round(dish.Price * (1 + percent / 100), 0);
                    totalCurrentRevenue += dish.Price * estimatedMonthlySales;
                    totalNewRevenue += newPrice * estimatedMonthlySales;
                }
                Console.WriteLine($"   ... và {dishes.Count - 3} món khác");
            }
            else
            {
                // Tính toán cho tất cả các món (nếu có ít hơn 3 món)
                foreach (var dish in dishes)
                {
                    decimal estimatedMonthlySales = dish.SalesCount > 0 ? dish.SalesCount / 3.0m : 10;
                    decimal newPrice = Math.Round(dish.Price * (1 + percent / 100), 0);
                    totalCurrentRevenue += dish.Price * estimatedMonthlySales;
                    totalNewRevenue += newPrice * estimatedMonthlySales;
                }
            }

            decimal totalRevenueChange = totalNewRevenue - totalCurrentRevenue;

            Console.WriteLine($"\n   📈 Ước tính thay đổi doanh thu hàng tháng:");
            Console.WriteLine($"      - Hiện tại: {totalCurrentRevenue:N0}đ");
            Console.WriteLine($"      - Sau thay đổi: {totalNewRevenue:N0}đ");
            Console.WriteLine($"      - Chênh lệch: {totalRevenueChange:+#;-#;0}đ");

            if (dishes.Any(d => d.SalesCount == 0))
            {
                Console.WriteLine($"   💡 Lưu ý: Ước tính dựa trên số lượt bán trung bình 10 lượt/tháng cho món mới");
            }
        }

        private void DisplayAvailabilityPreview(List<Dish> dishes, bool? newStatus)
        {
            Console.WriteLine($"\n📋 MÓN SẼ ĐƯỢC CẬP NHẬT:");

            foreach (var dish in dishes.Take(5))
            {
                string currentStatus = dish.IsAvailable ? "✅" : "❌";
                string futureStatus = newStatus.HasValue ? (newStatus.Value ? "✅" : "❌") : (dish.IsAvailable ? "❌" : "✅");
                Console.WriteLine($"   {currentStatus} → {futureStatus} {dish.Name}");
            }
            if (dishes.Count > 5)
            {
                Console.WriteLine($"   ... và {dishes.Count - 5} món khác");
            }
        }

        private bool ConfirmBatchUpdate(string actionDescription)
        {
            Console.WriteLine($"\n⚠️  BẠN SẮP {actionDescription.ToUpper()}");
            Console.WriteLine("   Thao tác này có thể ảnh hưởng đến doanh thu và không thể hoàn tác dễ dàng!");
            return EnhancedUI.Confirm("XÁC NHẬN thực hiện cập nhật hàng loạt?");
        }

        private void ExecuteBatchPriceUpdate(List<Dish> dishesToUpdate, decimal percent, string description)
        {
            var oldDishesState = dishesToUpdate.Select(d => new Dish(d.Id, d.Name, d.Description, d.Price, d.Category)
            {
                IsAvailable = d.IsAvailable,
                Ingredients = new Dictionary<string, decimal>(d.Ingredients),
                SalesCount = d.SalesCount,
                Cost = d.Cost
            }).ToList();

            foreach (var dish in dishesToUpdate)
            {
                dish.Price = dish.Price * (1 + percent / 100);
            }

            var command = new BatchUpdateDishesCommand(this, oldDishesState, dishesToUpdate);
            undoRedoService.ExecuteCommand(command);

            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "BATCH_UPDATE_PRICES", "DISH", "",
                description));
            SaveAllData();
        }

        private void ExecuteBatchAvailabilityUpdate(List<Dish> dishesToUpdate, bool? newStatus, string description)
        {
            var oldDishesState = dishesToUpdate.Select(d => new Dish(d.Id, d.Name, d.Description, d.Price, d.Category)
            {
                IsAvailable = d.IsAvailable,
                Ingredients = new Dictionary<string, decimal>(d.Ingredients),
                SalesCount = d.SalesCount,
                Cost = d.Cost
            }).ToList();

            foreach (var dish in dishesToUpdate)
            {
                dish.IsAvailable = newStatus.HasValue ? newStatus.Value : !dish.IsAvailable;
            }

            var command = new BatchUpdateDishesCommand(this, oldDishesState, dishesToUpdate);
            undoRedoService.ExecuteCommand(command);

            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "BATCH_UPDATE_AVAILABILITY", "DISH", "",
                description));
            SaveAllData();
        }

        private void ExecuteBatchCategoryUpdate(List<Dish> dishesToUpdate, string newCategory, string description)
        {
            var oldDishesState = dishesToUpdate.Select(d => new Dish(d.Id, d.Name, d.Description, d.Price, d.Category)
            {
                IsAvailable = d.IsAvailable,
                Ingredients = new Dictionary<string, decimal>(d.Ingredients),
                SalesCount = d.SalesCount,
                Cost = d.Cost
            }).ToList();

            foreach (var dish in dishesToUpdate)
            {
                dish.Category = newCategory;
            }

            var command = new BatchUpdateDishesCommand(this, oldDishesState, dishesToUpdate);
            undoRedoService.ExecuteCommand(command);

            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "BATCH_UPDATE_CATEGORIES", "DISH", "",
                description));
            SaveAllData();
        }

        private void DisplayUpdateSummary(List<Dish> updatedDishes)
        {
            Console.WriteLine($"\n📈 TÓM TẮT CẬP NHẬT:");
            Console.WriteLine($"   • Giá trung bình mới: {updatedDishes.Average(d => d.Price):N0}đ");
            Console.WriteLine($"   • Giá cao nhất: {updatedDishes.Max(d => d.Price):N0}đ");
            Console.WriteLine($"   • Giá thấp nhất: {updatedDishes.Min(d => d.Price):N0}đ");
        }

        private void BatchApplyDiscountByPrice()
        {
            EnhancedUI.DisplayHeader("🏷️ ÁP DỤNG KHUYẾN MÃI THEO GIÁ  ");

            // Hiển thị thống kê giá hiện tại
            DisplayPriceStatistics();

            Console.WriteLine("\n🎯 THIẾT LẬP CHIẾN LƯỢC KHUYẾN MÃI:");

            // Chọn chiến lược khuyến mãi
            Console.WriteLine("1. 💰 Giảm giá theo phần trăm");
            Console.WriteLine("2. 🔥 Giảm giá cố định (VNĐ)");
            Console.WriteLine("3. 🎯 Giảm giá phân cấp (nhiều mức)");
            Console.WriteLine("4. ⚡ Flash sale (giảm sâu món giá cao)");
            Console.Write("Lựa chọn chiến lược: ");

            string strategyChoice = Console.ReadLine();
            switch (strategyChoice)
            {
                case "1": ApplyPercentDiscount(); break;
                case "2": ApplyFixedDiscount(); break;
                case "3": ApplyTieredDiscount(); break;
                case "4": ApplyFlashSale(); break;
                default:
                    EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                    return;
            }
        }

        private void ApplyPercentDiscount()
        {
            Console.WriteLine("\n📊 CHIẾN LƯỢC: GIẢM GIÁ THEO PHẦN TRĂM");

            // Chọn điều kiện áp dụng
            var (targetDishes, conditionInfo) = SelectDiscountConditions();
            if (!targetDishes.Any())
            {
                EnhancedUI.DisplayWarning("Không tìm thấy món ăn nào phù hợp!");
                return;
            }

            // Nhập phần trăm giảm giá
            Console.Write("\n💸 Nhập phần trăm giảm giá (0-100%): ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal discountPercent) || discountPercent < 0 || discountPercent > 100)
            {
                EnhancedUI.DisplayError("Phần trăm giảm giá không hợp lệ!");
                return;
            }

            // Hiển thị xem trước
            DisplayDiscountPreview(targetDishes, discountPercent, "percent");

            // Xác nhận và thực hiện
            if (ConfirmDiscountApplication(targetDishes.Count, discountPercent, conditionInfo))
            {
                ExecutePercentDiscount(targetDishes, discountPercent, conditionInfo);
                EnhancedUI.DisplaySuccess($"✅ Đã áp dụng giảm {discountPercent}% cho {targetDishes.Count} món ăn!");
                DisplayDiscountSummary(targetDishes);
            }
        }

        private void ApplyFixedDiscount()
        {
            Console.WriteLine("\n📊 CHIẾN LƯỢC: GIẢM GIÁ CỐ ĐỊNH");

            var (targetDishes, conditionInfo) = SelectDiscountConditions();
            if (!targetDishes.Any())
            {
                EnhancedUI.DisplayWarning("Không tìm thấy món ăn nào phù hợp!");
                return;
            }

            // Nhập số tiền giảm cố định
            Console.Write("\n💸 Nhập số tiền giảm giá cố định (VNĐ): ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal discountAmount) || discountAmount < 0)
            {
                EnhancedUI.DisplayError("Số tiền giảm giá không hợp lệ!");
                return;
            }

            // Kiểm tra không vượt quá giá gốc
            var invalidDishes = targetDishes.Where(d => d.Price <= discountAmount).ToList();
            if (invalidDishes.Any())
            {
                Console.WriteLine($"\n⚠️ CẢNH BÁO: {invalidDishes.Count} món có giá thấp hơn số tiền giảm:");
                foreach (var dish in invalidDishes.Take(3))
                {
                    Console.WriteLine($"   - {dish.Name}: {dish.Price:N0}đ (giảm {discountAmount:N0}đ)");
                }

                if (!EnhancedUI.Confirm("Vẫn tiếp tục áp dụng? (Một số món có thể có giá âm)"))
                    return;
            }

            DisplayDiscountPreview(targetDishes, discountAmount, "fixed");

            if (ConfirmDiscountApplication(targetDishes.Count, discountAmount, conditionInfo))
            {
                ExecuteFixedDiscount(targetDishes, discountAmount, conditionInfo);
                EnhancedUI.DisplaySuccess($"✅ Đã giảm {discountAmount:N0}đ cho {targetDishes.Count} món ăn!");
                DisplayDiscountSummary(targetDishes);
            }
        }

        private void ApplyTieredDiscount()
        {
            Console.WriteLine("\n📊 CHIẾN LƯỢC: GIẢM GIÁ PHÂN CẤP");

            // Phân nhóm theo giá
            var priceGroups = repository.Dishes.Values
                .GroupBy(d => GetPriceTier(d.Price))
                .OrderBy(g => g.Key)
                .ToList();

            Console.WriteLine("\n📈 PHÂN NHÓM GIÁ HIỆN TẠI:");
            foreach (var group in priceGroups)
            {
                decimal avgPrice = group.Average(d => d.Price);
                Console.WriteLine($"   {group.Key}: {group.Count()} món (giá TB: {avgPrice:N0}đ)");
            }

            // Thiết lập mức giảm cho từng nhóm
            var tierDiscounts = new Dictionary<string, decimal>();
            Console.WriteLine("\n🎯 THIẾT LẬP MỨC GIẢM CHO TỪNG NHÓM:");

            foreach (var group in priceGroups)
            {
                Console.Write($"   Nhóm {group.Key} - {group.Count()} món: Giảm (%) = ");
                if (decimal.TryParse(Console.ReadLine(), out decimal discount) && discount >= 0 && discount <= 100)
                {
                    tierDiscounts[group.Key] = discount;
                }
                else
                {
                    EnhancedUI.DisplayError("Phần trăm không hợp lệ! Bỏ qua nhóm này.");
                }
            }

            // Lấy danh sách món sẽ được giảm giá
            var targetDishes = repository.Dishes.Values
                .Where(d => tierDiscounts.ContainsKey(GetPriceTier(d.Price)))
                .ToList();

            if (!targetDishes.Any())
            {
                EnhancedUI.DisplayWarning("Không có món nào được chọn để giảm giá!");
                return;
            }

            // Hiển thị xem trước
            DisplayTieredDiscountPreview(targetDishes, tierDiscounts);

            if (ConfirmTieredDiscountApplication(targetDishes.Count, tierDiscounts))
            {
                ExecuteTieredDiscount(targetDishes, tierDiscounts);
                EnhancedUI.DisplaySuccess($"✅ Đã áp dụng giảm giá phân cấp cho {targetDishes.Count} món!");
                DisplayDiscountSummary(targetDishes);
            }
        }

        private void ApplyFlashSale()
        {
            Console.WriteLine("\n📊 CHIẾN LƯỢC: FLASH SALE");

            // Chọn tiêu chí flash sale
            Console.WriteLine("🎯 CHỌN MÓN CHO FLASH SALE:");
            Console.WriteLine("1. 🔥 Món bán chạy nhất (top 10)");
            Console.WriteLine("2. 💎 Món cao cấp (giá > 200,000đ)");
            Console.WriteLine("3. 🌟 Món ít bán nhất (cần đẩy doanh số)");
            Console.WriteLine("4. 📦 Món có nguyên liệu sắp hết");
            Console.Write("Lựa chọn: ");

            List<Dish> flashSaleDishes = new List<Dish>();
            string flashSaleType = "";

            switch (Console.ReadLine())
            {
                case "1":
                    flashSaleDishes = repository.Dishes.Values
                        .OrderByDescending(d => d.SalesCount)
                        .Take(10)
                        .ToList();
                    flashSaleType = "món bán chạy nhất";
                    break;
                case "2":
                    flashSaleDishes = repository.Dishes.Values
                        .Where(d => d.Price > 200000)
                        .ToList();
                    flashSaleType = "món cao cấp";
                    break;
                case "3":
                    flashSaleDishes = repository.Dishes.Values
                        .OrderBy(d => d.SalesCount)
                        .Take(15)
                        .ToList();
                    flashSaleType = "món ít bán";
                    break;
                case "4":
                    flashSaleDishes = repository.Dishes.Values
                        .Where(d => !CheckDishIngredients(d) || d.Ingredients.Any(ing =>
                            repository.Ingredients.ContainsKey(ing.Key) &&
                            repository.Ingredients[ing.Key].IsLowStock))
                        .ToList();
                    flashSaleType = "món có nguyên liệu sắp hết";
                    break;
                default:
                    EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                    return;
            }

            if (!flashSaleDishes.Any())
            {
                EnhancedUI.DisplayWarning($"Không tìm thấy {flashSaleType} nào!");
                return;
            }

            // Thiết lập mức giảm flash sale
            Console.Write("\n💥 Nhập phần trăm giảm giá FLASH SALE (20-80%): ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal flashDiscount) || flashDiscount < 20 || flashDiscount > 80)
            {
                EnhancedUI.DisplayError("Mức giảm flash sale phải từ 20-80%!");
                return;
            }

            // Hiển thị thông tin flash sale
            Console.WriteLine($"\n🎉 THÔNG TIN FLASH SALE:");
            Console.WriteLine($"   • Loại: {flashSaleType}");
            Console.WriteLine($"   • Số món: {flashSaleDishes.Count}");
            Console.WriteLine($"   • Giảm giá: {flashDiscount}%");
            Console.WriteLine($"   • Thời gian: Ngay lập tức");

            DisplayFlashSalePreview(flashSaleDishes, flashDiscount);

            if (ConfirmFlashSaleApplication(flashSaleDishes.Count, flashDiscount, flashSaleType))
            {
                ExecutePercentDiscount(flashSaleDishes, flashDiscount, $"Flash sale {flashSaleType}");
                EnhancedUI.DisplaySuccess($"🎉 FLASH SALE THÀNH CÔNG! Đã giảm {flashDiscount}% cho {flashSaleDishes.Count} món!");

                // Hiển thị kết quả đặc biệt cho flash sale
                DisplayFlashSaleResults(flashSaleDishes);
            }
        }

        // ==================== HELPER METHODS ====================

        private (List<Dish> dishes, string conditionInfo) SelectDiscountConditions()
        {
            Console.WriteLine("\n🎯 ĐIỀU KIỆN ÁP DỤNG:");
            Console.WriteLine("1. 📂 Theo nhóm món");
            Console.WriteLine("2. 💰 Theo khoảng giá");
            Console.WriteLine("3. 📈 Món có lợi nhuận cao (>30%)");
            Console.WriteLine("4. 🔥 Món bán chạy (>20 lượt)");
            Console.WriteLine("5. 🌟 Tất cả món ăn");
            Console.Write("Lựa chọn: ");

            string conditionChoice = Console.ReadLine();
            switch (conditionChoice)
            {
                case "1":
                    Console.Write("Nhập tên nhóm món: ");
                    string category = Console.ReadLine()?.Trim();
                    var categoryDishes = repository.Dishes.Values
                        .Where(d => d.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    return (categoryDishes, $"nhóm '{category}'");

                case "2":
                    Console.Write("Giá tối thiểu (để trống nếu không giới hạn): ");
                    decimal min;
                    decimal? minPrice = null;
                    string input = Console.ReadLine();

                    if (decimal.TryParse(input, out min))
                    {
                        minPrice = min;
                    }
                    Console.Write("Giá tối đa (để trống nếu không giới hạn): ");
                    decimal max;
                    decimal? maxPrice = null;
                    string input2 = Console.ReadLine();

                    if (decimal.TryParse(input2, out max))
                    {
                        maxPrice = max;
                    }

                    var priceDishes = repository.Dishes.Values.Where(d =>
                        (!minPrice.HasValue || d.Price >= minPrice.Value) &&
                        (!maxPrice.HasValue || d.Price <= maxPrice.Value)).ToList();

                    string rangeInfo = "";
                    if (minPrice.HasValue && maxPrice.HasValue)
                        rangeInfo = $"giá từ {minPrice.Value:N0}đ đến {maxPrice.Value:N0}đ";
                    else if (minPrice.HasValue)
                        rangeInfo = $"giá từ {minPrice.Value:N0}đ";
                    else if (maxPrice.HasValue)
                        rangeInfo = $"giá đến {maxPrice.Value:N0}đ";

                    return (priceDishes, rangeInfo);

                case "3":
                    var highProfitDishes = repository.Dishes.Values
                        .Where(d => d.ProfitMargin > 30)
                        .ToList();
                    return (highProfitDishes, "lợi nhuận cao");

                case "4":
                    var popularDishes = repository.Dishes.Values
                        .Where(d => d.SalesCount > 20)
                        .ToList();
                    return (popularDishes, "bán chạy");

                case "5":
                    return (repository.Dishes.Values.ToList(), "tất cả món");

                default:
                    EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                    return (new List<Dish>(), "");
            }
        }

        private string GetPriceTier(decimal price)
        {
            if (price < 50000) return "Dưới 50k";
            if (price < 100000) return "50k-100k";
            if (price < 200000) return "100k-200k";
            if (price < 500000) return "200k-500k";
            return "Trên 500k";
        }

        private void DisplayDiscountPreview(List<Dish> dishes, decimal discountValue, string discountType)
        {
            Console.WriteLine($"\n👀 XEM TRƯỚC KHUYẾN MÃI:");

            foreach (var dish in dishes.Take(3))
            {
                decimal newPrice = discountType == "percent"
                    ? dish.Price * (1 - discountValue / 100)
                    : Math.Max(0, dish.Price - discountValue);

                decimal discountAmount = dish.Price - newPrice;
                decimal actualDiscountPercent = (discountAmount / dish.Price) * 100;

                Console.WriteLine($"   - {dish.Name}:");
                Console.WriteLine($"     {dish.Price:N0}đ → {newPrice:N0}đ");
                Console.WriteLine($"     📉 Giảm: {discountAmount:N0}đ ({actualDiscountPercent:N1}%)");
            }

            if (dishes.Count > 3)
            {
                Console.WriteLine($"   ... và {dishes.Count - 3} món khác");
            }

            // Tính toán ảnh hưởng doanh thu
            decimal totalRevenueLoss = dishes.Sum(d =>
                discountType == "percent"
                    ? d.Price * (discountValue / 100) * d.SalesCount
                    : discountValue * d.SalesCount);

            decimal avgDiscountPercent = discountType == "percent"
                ? discountValue
                : (discountValue / dishes.Average(d => d.Price)) * 100;

            Console.WriteLine($"\n📊 ƯỚC TÍNH ẢNH HƯỞNG:");
            Console.WriteLine($"   • Tổng giảm giá: {totalRevenueLoss:N0}đ");
            Console.WriteLine($"   • Giảm giá trung bình: {avgDiscountPercent:N1}%");
            Console.WriteLine($"   • Số món được giảm: {dishes.Count}");
        }

        private void DisplayTieredDiscountPreview(List<Dish> dishes, Dictionary<string, decimal> tierDiscounts)
        {
            Console.WriteLine($"\n👀 XEM TRƯỚC GIẢM GIÁ PHÂN CẤP:");

            var groupedDishes = dishes.GroupBy(d => GetPriceTier(d.Price))
                                     .OrderBy(g => g.Key);

            foreach (var group in groupedDishes)
            {
                decimal discountPercent = tierDiscounts[group.Key];
                decimal avgOriginalPrice = group.Average(d => d.Price);
                decimal avgNewPrice = avgOriginalPrice * (1 - discountPercent / 100);

                Console.WriteLine($"   📊 Nhóm {group.Key}:");
                Console.WriteLine($"     • Số món: {group.Count()}");
                Console.WriteLine($"     • Giá TB: {avgOriginalPrice:N0}đ → {avgNewPrice:N0}đ");
                Console.WriteLine($"     • Giảm: {discountPercent}%");

                foreach (var dish in group.Take(2))
                {
                    decimal newPrice = dish.Price * (1 - discountPercent / 100);
                    Console.WriteLine($"       - {dish.Name}: {dish.Price:N0}đ → {newPrice:N0}đ");
                }
                if (group.Count() > 2)
                {
                    Console.WriteLine($"       ... và {group.Count() - 2} món khác");
                }
            }
        }

        private void DisplayFlashSalePreview(List<Dish> dishes, decimal discountPercent)
        {
            Console.WriteLine($"\n🎉 DANH SÁCH FLASH SALE:");

            foreach (var dish in dishes.Take(5))
            {
                decimal newPrice = dish.Price * (1 - discountPercent / 100);
                decimal savings = dish.Price - newPrice;

                Console.WriteLine($"   🔥 {dish.Name}");
                Console.WriteLine($"      {dish.Price:N0}đ → {newPrice:N0}đ");
                Console.WriteLine($"      💰 Tiết kiệm: {savings:N0}đ");
                Console.WriteLine($"      📊 Đã bán: {dish.SalesCount} lượt");
            }

            if (dishes.Count > 5)
            {
                Console.WriteLine($"   ... và {dishes.Count - 5} món khác");
            }

            decimal totalSavings = dishes.Sum(d => (d.Price * discountPercent / 100) * 10); // Ước tính 10 đơn mỗi món
            Console.WriteLine($"\n💎 DỰ KIẾN: Khách hàng tiết kiệm ~{totalSavings:N0}đ!");
        }

        private bool ConfirmDiscountApplication(int dishCount, decimal discountValue, string conditionInfo)
        {
            string discountText = discountValue <= 100 ? $"{discountValue}%" : $"{discountValue:N0}đ";

            Console.WriteLine($"\n⚠️  XÁC NHẬN ÁP DỤNG KHUYẾN MÃI");
            Console.WriteLine($"   • Số món: {dishCount} {conditionInfo}");
            Console.WriteLine($"   • Mức giảm: {discountText}");
            Console.WriteLine($"   • Ảnh hưởng: Có thể thay đổi doanh thu đáng kể");

            return EnhancedUI.Confirm("XÁC NHẬN áp dụng khuyến mãi?");
        }

        private bool ConfirmTieredDiscountApplication(int dishCount, Dictionary<string, decimal> tierDiscounts)
        {
            Console.WriteLine($"\n⚠️  XÁC NHẬN GIẢM GIÁ PHÂN CẤP");
            Console.WriteLine($"   • Tổng số món: {dishCount}");
            Console.WriteLine($"   • Các mức giảm:");
            foreach (var tier in tierDiscounts)
            {
                Console.WriteLine($"     - {tier.Key}: {tier.Value}%");
            }

            return EnhancedUI.Confirm("XÁC NHẬN áp dụng giảm giá phân cấp?");
        }

        private bool ConfirmFlashSaleApplication(int dishCount, decimal discountPercent, string saleType)
        {
            Console.WriteLine($"\n🎯 XÁC NHẬN FLASH SALE");
            Console.WriteLine($"   • Chiến dịch: {saleType}");
            Console.WriteLine($"   • Số món: {dishCount}");
            Console.WriteLine($"   • Giảm giá: {discountPercent}%");
            Console.WriteLine($"   • Thời gian: Áp dụng ngay lập tức");
            Console.WriteLine($"   • Lưu ý: Flash sale có thể gây sốt đơn hàng!");

            return EnhancedUI.Confirm("KHỞI CHẠY flash sale?");
        }

        private void ExecutePercentDiscount(List<Dish> dishes, decimal discountPercent, string conditionInfo)
        {
            var oldDishesState = dishes.Select(d => new Dish(d.Id, d.Name, d.Description, d.Price, d.Category)
            {
                IsAvailable = d.IsAvailable,
                Ingredients = new Dictionary<string, decimal>(d.Ingredients),
                SalesCount = d.SalesCount,
                Cost = d.Cost
            }).ToList();

            foreach (var dish in dishes)
            {
                dish.Price = dish.Price * (1 - discountPercent / 100);
            }

            var command = new BatchUpdateDishesCommand(this, oldDishesState, dishes);
            undoRedoService.ExecuteCommand(command);

            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "BATCH_DISCOUNT_PERCENT", "DISH", "",
                $"Giảm {discountPercent}% cho {dishes.Count} món {conditionInfo}"));
            SaveAllData();
        }

        private void ExecuteFixedDiscount(List<Dish> dishes, decimal discountAmount, string conditionInfo)
        {
            var oldDishesState = dishes.Select(d => new Dish(d.Id, d.Name, d.Description, d.Price, d.Category)
            {
                IsAvailable = d.IsAvailable,
                Ingredients = new Dictionary<string, decimal>(d.Ingredients),
                SalesCount = d.SalesCount,
                Cost = d.Cost
            }).ToList();

            foreach (var dish in dishes)
            {
                dish.Price = Math.Max(0, dish.Price - discountAmount);
            }

            var command = new BatchUpdateDishesCommand(this, oldDishesState, dishes);
            undoRedoService.ExecuteCommand(command);

            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "BATCH_DISCOUNT_FIXED", "DISH", "",
                $"Giảm {discountAmount:N0}đ cho {dishes.Count} món {conditionInfo}"));
            SaveAllData();
        }

        private void ExecuteTieredDiscount(List<Dish> dishes, Dictionary<string, decimal> tierDiscounts, string description = "giảm giá phân cấp")
        {
            var oldDishesState = dishes.Select(d => new Dish(d.Id, d.Name, d.Description, d.Price, d.Category)
            {
                IsAvailable = d.IsAvailable,
                Ingredients = new Dictionary<string, decimal>(d.Ingredients),
                SalesCount = d.SalesCount,
                Cost = d.Cost
            }).ToList();

            foreach (var dish in dishes)
            {
                string tier = GetPriceTier(dish.Price);
                if (tierDiscounts.ContainsKey(tier))
                {
                    dish.Price = dish.Price * (1 - tierDiscounts[tier] / 100);
                }
            }

            var command = new BatchUpdateDishesCommand(this, oldDishesState, dishes);
            undoRedoService.ExecuteCommand(command);

            string tierInfo = string.Join(", ", tierDiscounts.Select(t => $"{t.Key}({t.Value}%)"));
            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "BATCH_DISCOUNT_TIERED", "DISH", "",
                $"Giảm giá phân cấp {tierInfo} cho {dishes.Count} món"));
            SaveAllData();
        }

        private void DisplayDiscountSummary(List<Dish> updatedDishes)
        {
            Console.WriteLine($"\n📈 KẾT QUẢ KHUYẾN MÃI:");
            Console.WriteLine($"   • Giá trung bình mới: {updatedDishes.Average(d => d.Price):N0}đ");
            Console.WriteLine($"   • Giá thấp nhất: {updatedDishes.Min(d => d.Price):N0}đ");
            Console.WriteLine($"   • Giá cao nhất: {updatedDishes.Max(d => d.Price):N0}đ");

            decimal totalPotentialRevenue = updatedDishes.Sum(d => d.Price * 10); // Ước tính 10 đơn mỗi món
            Console.WriteLine($"   • 💎 Dự kiến tăng đơn: ~{totalPotentialRevenue:N0}đ doanh thu");
        }

        private void DisplayFlashSaleResults(List<Dish> flashSaleDishes)
        {
            Console.WriteLine($"\n🎊 FLASH SALE ĐÃ KÍCH HOẠT!");
            Console.WriteLine($"   • Số món: {flashSaleDishes.Count}");
            Console.WriteLine($"   • Giá trung bình: {flashSaleDishes.Average(d => d.Price):N0}đ");
            Console.WriteLine($"   • Tổng lượt bán: {flashSaleDishes.Sum(d => d.SalesCount)}");

            // Ước tính hiệu ứng flash sale
            decimal estimatedBoost = flashSaleDishes.Sum(d => d.Price * 0.3m); // Ước tính tăng 30% đơn hàng
            Console.WriteLine($"   • 🚀 Dự kiến tăng: +{estimatedBoost:N0}đ doanh thu");
            Console.WriteLine($"   • ⏰ Khuyến nghị: Theo dõi đơn hàng trong 24h tới");

            EnhancedUI.DisplaySuccess("🎯 Hãy quảng bá flash sale trên các kênh marketing!");
        }

        private void BatchUpdateAvailability()
        {
            EnhancedUI.DisplayHeader("🔄 CẬP NHẬT TRẠNG THÁI SẴN CÓ");

            // Hiển thị thống kê trạng thái hiện tại
            DisplayAvailabilityStatistics();

            Console.WriteLine("\n🎯 THIẾT LẬP CẬP NHẬT:");

            // Chọn phạm vi áp dụng
            string scope = SelectUpdateScope();
            if (scope == null) return;

            // Chọn trạng thái mới
            Console.WriteLine("\n📋 Chọn trạng thái mới:");
            Console.WriteLine("1. ✅ Có sẵn (Có thể đặt món)");
            Console.WriteLine("2. ❌ Tạm hết (Không thể đặt món)");
            Console.WriteLine("3. 🔄 Đảo ngược trạng thái hiện tại");
            Console.Write("Lựa chọn: ");

            string statusChoice = Console.ReadLine();
            bool? newStatus = null;
            string statusDescription = "";

            switch (statusChoice)
            {
                case "1":
                    newStatus = true;
                    statusDescription = "Có sẵn";
                    break;
                case "2":
                    newStatus = false;
                    statusDescription = "Tạm hết";
                    break;
                case "3":
                    statusDescription = "Đảo ngược trạng thái";
                    break;
                default:
                    EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                    return;
            }

            // Lấy danh sách món cần cập nhật
            var (dishesToUpdate, filterInfo) = GetDishesByScope(scope);
            if (!dishesToUpdate.Any())
            {
                EnhancedUI.DisplayWarning("Không tìm thấy món ăn nào phù hợp!");
                return;
            }

            // Hiển thị xem trước thay đổi
            DisplayAvailabilityPreview(dishesToUpdate, newStatus);

            // Xác nhận thực hiện
            if (ConfirmBatchUpdate($"cập nhật trạng thái '{statusDescription}' cho {dishesToUpdate.Count} món ăn{filterInfo}"))
            {
                ExecuteBatchAvailabilityUpdate(dishesToUpdate, newStatus,
                    $"Cập nhật trạng thái '{statusDescription}'{filterInfo}");

                EnhancedUI.DisplaySuccess($"✅ Đã cập nhật trạng thái thành công cho {dishesToUpdate.Count} món ăn!");
            }
        }

        private void BatchUpdatePricesByPercent()
        {
            EnhancedUI.DisplayHeader("📊 CẬP NHẬT GIÁ THEO PHẦN TRĂM");

            // Hiển thị thống kê hiện tại
            DisplayPriceStatistics();

            Console.WriteLine("\n🎯 THIẾT LẬP CẬP NHẬT:");

            // Chọn phạm vi áp dụng
            string scope = SelectUpdateScope();
            if (scope == null) return;

            // Nhập phần trăm thay đổi
            Console.Write("\n💵 Nhập phần trăm thay đổi giá (+ để tăng, - để giảm): ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal percent))
            {
                EnhancedUI.DisplayError("Phần trăm không hợp lệ!");
                return;
            }

            // Lấy danh sách món cần cập nhật
            var (dishesToUpdate, filterInfo) = GetDishesByScope(scope);
            if (!dishesToUpdate.Any())
            {
                EnhancedUI.DisplayWarning("Không tìm thấy món ăn nào phù hợp!");
                return;
            }

            // Hiển thị xem trước thay đổi
            DisplayPriceChangePreview(dishesToUpdate, percent);

            // Xác nhận thực hiện
            if (ConfirmBatchUpdate($"cập nhật giá {percent}% cho {dishesToUpdate.Count} món ăn{filterInfo}"))
            {
                ExecuteBatchPriceUpdate(dishesToUpdate, percent,
                    $"Cập nhật giá {percent}%{filterInfo}");

                EnhancedUI.DisplaySuccess($"✅ Đã cập nhật giá thành công cho {dishesToUpdate.Count} món ăn!");
                DisplayUpdateSummary(dishesToUpdate);
            }
        }



        private void AddDishesFromFile()
        {
            EnhancedUI.DisplayHeader("THÊM MÓN ĂN TỪ FILE");

            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                if (!Directory.Exists(downloadPath))
                {
                    EnhancedUI.DisplayError("Thư mục Downloads không tồn tại!");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"Các file trong thư mục Downloads:");
                var files = Directory.GetFiles(downloadPath, "*.txt").Concat(
                           Directory.GetFiles(downloadPath, "*.csv")).ToArray();

                if (!files.Any())
                {
                    EnhancedUI.DisplayError("Không tìm thấy file .txt hoặc .csv trong thư mục Downloads!");
                    Console.ReadKey();
                    return;
                }

                for (int i = 0; i < files.Length; i++)
                {
                    Console.WriteLine($"{i + 1}. {Path.GetFileName(files[i])}");
                }

                Console.Write("Chọn file (số): ");
                if (int.TryParse(Console.ReadLine(), out int fileIndex) && fileIndex > 0 && fileIndex <= files.Length)
                {
                    string filePath = files[fileIndex - 1];
                    ImportDishesFromFile(filePath);
                }
                else
                {
                    EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to add dishes from file", "DishManagement", ex);
                EnhancedUI.DisplayError($"Lỗi: {ex.Message}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

private void ImportDishesFromFile(string filePath)
    {
        try
        {
            int successCount = 0;
            int errorCount = 0;
            List<Dish> importedDishes = new List<Dish>();

            using (TextFieldParser parser = new TextFieldParser(filePath))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true; // ✅ Cho phép giá trị chứa dấu phẩy trong dấu ngoặc kép

                bool headerSkipped = false;

                while (!parser.EndOfData)
                {
                    string[] parts = parser.ReadFields();

                    if (!headerSkipped)
                    {
                        headerSkipped = true;
                        continue;
                    }

                    try
                    {
                        if (parts.Length >= 5)
                        {
                            string id = parts[0].Trim();
                            string name = parts[1].Trim();
                            string description = parts[2].Trim();
                            decimal price = decimal.Parse(parts[3].Trim());
                            string category = parts[4].Trim();

                            if (!repository.Dishes.ContainsKey(id))
                            {
                                var dish = new Dish(id, name, description, price, category);
                                importedDishes.Add(dish);
                                successCount++;
                            }
                            else
                            {
                                errorCount++;
                            }
                        }
                        else
                        {
                            errorCount++;
                        }
                    }
                    catch
                    {
                        errorCount++;
                    }
                }
            }

            if (importedDishes.Any())
            {
                var command = new BatchAddDishesCommand(this, importedDishes);
                undoRedoService.ExecuteCommand(command);

                repository.AuditLogs.Add(new AuditLog(currentUser.Username, "IMPORT_DISHES", "DISH", "", $"Nhập từ file: {Path.GetFileName(filePath)}"));
                SaveAllData();
            }

            EnhancedUI.DisplaySuccess($"Nhập dữ liệu thành công: {successCount} món");
            if (errorCount > 0)
            {
                EnhancedUI.DisplayWarning($"Có {errorCount} món bị lỗi hoặc trùng mã");
            }

            Logger.Info($"Imported {successCount} dishes from {filePath}", "DishManagement");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to import dishes from {filePath}", "DishManagement", ex);
            EnhancedUI.DisplayError($"Lỗi khi đọc file: {ex.Message}");
        }
    }


        // ==================== INGREDIENT MANAGEMENT METHODS ====================
        private void UpdateIngredient(int page = 1, int pageSize = 10)
        {
            EnhancedUI.DisplayHeader("CẬP NHẬT NGUYÊN LIỆU");

            var ingredients = repository.Ingredients.Values.ToList();
            int totalPages = (int)Math.Ceiling(ingredients.Count / (double)pageSize);

            if (ingredients.Count == 0)
            {
                EnhancedUI.DisplayInfo("Chưa có nguyên liệu nào trong hệ thống!");
                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
                return;
            }

            // Hiển thị danh sách theo trang
            var paged = ingredients.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            Console.WriteLine("═════════════════════════════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"{"MÃ",-8} {"TÊN NGUYÊN LIỆU",-25} {"ĐƠN VỊ",-10} {"SỐ LƯỢNG",-10} {"TỐI THIỂU",-10} {"GIÁ",-12}");
            Console.WriteLine("═════════════════════════════════════════════════════════════════════════════════════════════");

            foreach (var ing in paged)
            {
                Console.WriteLine($"{ing.Id,-8} {TruncateString(ing.Name, 25),-25} {ing.Unit,-10} {ing.Quantity,-10} {ing.MinQuantity,-10} {ing.PricePerUnit,12:N0}");
            }

            Console.WriteLine("═════════════════════════════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"Trang {page}/{totalPages} - Tổng cộng {ingredients.Count} nguyên liệu");
            Console.WriteLine("Nhập số trang để chuyển | Nhập mã nguyên liệu để cập nhật | 0 để thoát");
            Console.Write("\nLựa chọn: ");
            string input = Console.ReadLine()?.Trim();

            // Thoát
            if (input == "0") return;

            // Chuyển trang
            if (int.TryParse(input, out int newPage))
            {
                if (newPage >= 1 && newPage <= totalPages)
                {
                    UpdateIngredient(newPage, pageSize);
                }
                else
                {
                    EnhancedUI.DisplayError("❌ Số trang không hợp lệ!");
                    Console.ReadKey();
                    UpdateIngredient(page, pageSize);
                }
                return;
            }

            // Nếu không phải số thì xem như nhập mã nguyên liệu
            string ingId = input;
            if (!repository.Ingredients.ContainsKey(ingId))
            {
                EnhancedUI.DisplayError("❌ Không tìm thấy nguyên liệu!");
                Console.ReadKey();
                UpdateIngredient(page, pageSize);
                return;
            }

            var oldIng = repository.Ingredients[ingId];
            var newIng = new Ingredient(oldIng.Id, oldIng.Name, oldIng.Unit,
                oldIng.Quantity, oldIng.MinQuantity, oldIng.PricePerUnit);

            try
            {
                Console.Clear();
                EnhancedUI.DisplayHeader($"CẬP NHẬT NGUYÊN LIỆU [{oldIng.Id}]");

                Console.WriteLine("(Để trống nếu giữ nguyên)");

                Console.Write($"Tên nguyên liệu ({oldIng.Name}): ");
                string name = Console.ReadLine();
                if (!string.IsNullOrEmpty(name)) newIng.Name = name;

                Console.Write($"Đơn vị tính ({oldIng.Unit}): ");
                string unit = Console.ReadLine();
                if (!string.IsNullOrEmpty(unit)) newIng.Unit = unit;

                Console.Write($"Số lượng ({oldIng.Quantity}): ");
                string quantityStr = Console.ReadLine();
                if (!string.IsNullOrEmpty(quantityStr) && decimal.TryParse(quantityStr, out decimal quantity))
                    newIng.Quantity = quantity;

                Console.Write($"Số lượng tối thiểu ({oldIng.MinQuantity}): ");
                string minStr = Console.ReadLine();
                if (!string.IsNullOrEmpty(minStr) && decimal.TryParse(minStr, out decimal min))
                    newIng.MinQuantity = min;

                Console.Write($"Giá mỗi đơn vị ({oldIng.PricePerUnit:N0}đ): ");
                string priceStr = Console.ReadLine();
                if (!string.IsNullOrEmpty(priceStr) && decimal.TryParse(priceStr, out decimal price))
                    newIng.PricePerUnit = price;

                var command = new UpdateIngredientCommand(this, oldIng, newIng);
                undoRedoService.ExecuteCommand(command);

                repository.AuditLogs.Add(new AuditLog(currentUser.Username, "UPDATE_INGREDIENT", "INGREDIENT", ingId, $"Cập nhật nguyên liệu: {newIng.Name}"));
                SaveAllData();

                EnhancedUI.DisplaySuccess("✅ Cập nhật nguyên liệu thành công!");
                Logger.Info($"Ingredient {ingId} updated", "IngredientManagement");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to update ingredient {ingId}", "IngredientManagement", ex);
                EnhancedUI.DisplayError($"Lỗi: {ex.Message}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để quay lại danh sách...");
            Console.ReadKey();
            UpdateIngredient(page, pageSize);
        }


        private void DeleteIngredient(int page = 1, int pageSize = 10)
        {
            EnhancedUI.DisplayHeader("XÓA NGUYÊN LIỆU");

            var ingredients = repository.Ingredients.Values.ToList();
            int totalPages = (int)Math.Ceiling(ingredients.Count / (double)pageSize);

            if (ingredients.Count == 0)
            {
                EnhancedUI.DisplayInfo("Không có nguyên liệu nào trong hệ thống!");
                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
                return;
            }

            // Phân trang hiển thị
            var paged = ingredients.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"{"MÃ",-8} {"TÊN NGUYÊN LIỆU",-25} {"ĐƠN VỊ",-10} {"SỐ LƯỢNG",-10} {"TỐI THIỂU",-10} {"GIÁ",-12}");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════════════════════");

            foreach (var ing in paged)
            {
                Console.WriteLine($"{ing.Id,-8} {TruncateString(ing.Name, 25),-25} {ing.Unit,-10} {ing.Quantity,-10} {ing.MinQuantity,-10} {ing.PricePerUnit,12:N0}");
            }

            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"Trang {page}/{totalPages} - Tổng cộng {ingredients.Count} nguyên liệu");
            Console.WriteLine("\n🔹 Nhập số trang để chuyển");
            Console.WriteLine("🔹 Nhập từ khóa để tìm (tên hoặc mã)");
            Console.WriteLine("🔹 Nhập mã nguyên liệu (cách nhau dấu ,) để xóa");
            Console.WriteLine("🔹 Nhập 0 để thoát");
            Console.Write("\nLựa chọn: ");

            string input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) { DeleteIngredient(page, pageSize); return; }

            // Thoát
            if (input == "0") return;

            // Chuyển trang
            if (int.TryParse(input, out int newPage))
            {
                if (newPage >= 1 && newPage <= totalPages)
                {
                    DeleteIngredient(newPage, pageSize);
                }
                else
                {
                    EnhancedUI.DisplayError("❌ Số trang không hợp lệ!");
                    Console.ReadKey();
                    DeleteIngredient(page, pageSize);
                }
                return;
            }

            // Tìm kiếm
            if (!input.Contains(",") && !repository.Ingredients.ContainsKey(input))
            {
                var searchResults = ingredients
                    .Where(i => i.Name.ToLower().Contains(input.ToLower()) || i.Id.ToLower().Contains(input.ToLower()))
                    .ToList();

                if (!searchResults.Any())
                {
                    EnhancedUI.DisplayError("❌ Không tìm thấy nguyên liệu nào!");
                    Console.ReadKey();
                    DeleteIngredient(page, pageSize);
                    return;
                }

                Console.Clear();
                EnhancedUI.DisplayHeader($"KẾT QUẢ TÌM KIẾM: {input}");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════════════════════");
                Console.WriteLine($"{"MÃ",-8} {"TÊN NGUYÊN LIỆU",-25} {"ĐƠN VỊ",-10} {"SỐ LƯỢNG",-10} {"TỐI THIỂU",-10} {"GIÁ",-12}");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════════════════════");

                foreach (var ing in searchResults)
                {
                    Console.WriteLine($"{ing.Id,-8} {TruncateString(ing.Name, 25),-25} {ing.Unit,-10} {ing.Quantity,-10} {ing.MinQuantity,-10} {ing.PricePerUnit,12:N0}");
                }

                Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════════════════════");
                Console.Write("\nNhập mã nguyên liệu (hoặc nhiều mã cách nhau dấu ,) để xóa: ");
                input = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(input)) { DeleteIngredient(page, pageSize); return; }
            }

            // Xử lý xóa hàng loạt
            var idsToDelete = input.Split(',').Select(id => id.Trim()).Where(id => !string.IsNullOrEmpty(id)).ToList();
            int deletedCount = 0;

            foreach (var id in idsToDelete)
            {
                if (!repository.Ingredients.ContainsKey(id))
                {
                    EnhancedUI.DisplayError($"❌ Nguyên liệu '{id}' không tồn tại!");
                    continue;
                }

                var ingredient = repository.Ingredients[id];
                bool isUsed = repository.Dishes.Values.Any(d => d.Ingredients.ContainsKey(id));

                if (isUsed)
                {
                    EnhancedUI.DisplayError($"⚠️ Không thể xóa '{ingredient.Name}' vì đang được sử dụng trong món ăn!");
                    continue;
                }

                if (EnhancedUI.Confirm($"Xác nhận xóa nguyên liệu '{ingredient.Name}'?"))
                {
                    try
                    {
                        var command = new DeleteIngredientCommand(this, ingredient);
                        undoRedoService.ExecuteCommand(command);

                        repository.AuditLogs.Add(new AuditLog(currentUser.Username, "DELETE_INGREDIENT", "INGREDIENT", id, $"Xóa nguyên liệu: {ingredient.Name}"));
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to delete ingredient {id}", "IngredientManagement", ex);
                        EnhancedUI.DisplayError($"Lỗi khi xóa {ingredient.Name}: {ex.Message}");
                    }
                }
            }

            if (deletedCount > 0)
            {
                SaveAllData();
                EnhancedUI.DisplaySuccess($"✅ Đã xóa {deletedCount} nguyên liệu thành công!");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để quay lại danh sách...");
            Console.ReadKey();
            DeleteIngredient(page, pageSize);
        }


        private void BatchUpdateIngredients()
        {
            EnhancedUI.DisplayHeader("CẬP NHẬT NGUYÊN LIỆU HÀNG LOẠT");

            Console.WriteLine("1. Cập nhật giá theo phần trăm");
            Console.WriteLine("2. Cập nhật số lượng tồn kho");
            Console.WriteLine("3. Cập nhật số lượng tối thiểu");
            Console.Write("Chọn loại cập nhật: ");

            string choice = Console.ReadLine();
            List<Ingredient> ingredientsToUpdate = new List<Ingredient>();
            List<Ingredient> oldIngredientsState = new List<Ingredient>();

            try
            {
                switch (choice)
                {
                    case "1":
                        Console.Write("Nhập phần trăm thay đổi giá (+ để tăng, - để giảm): ");
                        if (!decimal.TryParse(Console.ReadLine(), out decimal percent))
                        {
                            EnhancedUI.DisplayError("Phần trăm không hợp lệ!");
                            return;
                        }

                        ingredientsToUpdate = repository.Ingredients.Values.ToList();

                        // Lưu trạng thái cũ
                        foreach (var ing in ingredientsToUpdate)
                        {
                            oldIngredientsState.Add(new Ingredient(ing.Id, ing.Name, ing.Unit, ing.Quantity, ing.MinQuantity, ing.PricePerUnit));
                        }

                        Console.WriteLine($"Sẽ cập nhật {ingredientsToUpdate.Count} nguyên liệu");
                        if (EnhancedUI.Confirm("Xác nhận cập nhật?"))
                        {
                            foreach (var ing in ingredientsToUpdate)
                            {
                                ing.PricePerUnit = ing.PricePerUnit * (1 + percent / 100);
                            }

                            // Tạo command cho từng nguyên liệu (vì không có BatchUpdateIngredientsCommand)
                            foreach (var ing in ingredientsToUpdate)
                            {
                                var oldIng = oldIngredientsState.FirstOrDefault(o => o.Id == ing.Id);
                                if (oldIng != null)
                                {
                                    var command = new UpdateIngredientCommand(this, oldIng, ing);
                                    undoRedoService.ExecuteCommand(command);
                                }
                            }

                            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "BATCH_UPDATE_INGREDIENTS", "INGREDIENT", "",
                                $"Cập nhật {ingredientsToUpdate.Count} nguyên liệu, thay đổi {percent}%"));
                            SaveAllData();

                            EnhancedUI.DisplaySuccess("Cập nhật giá hàng loạt thành công!");
                        }
                        break;

                    case "2":
                        Console.Write("Nhập số lượng cộng thêm (+ để thêm, - để bớt): ");
                        if (!decimal.TryParse(Console.ReadLine(), out decimal quantityChange))
                        {
                            EnhancedUI.DisplayError("Số lượng không hợp lệ!");
                            return;
                        }

                        ingredientsToUpdate = repository.Ingredients.Values.ToList();

                        // Lưu trạng thái cũ
                        foreach (var ing in ingredientsToUpdate)
                        {
                            oldIngredientsState.Add(new Ingredient(ing.Id, ing.Name, ing.Unit, ing.Quantity, ing.MinQuantity, ing.PricePerUnit));
                        }

                        Console.WriteLine($"Sẽ cập nhật {ingredientsToUpdate.Count} nguyên liệu");
                        if (EnhancedUI.Confirm("Xác nhận cập nhật?"))
                        {
                            foreach (var ing in ingredientsToUpdate)
                            {
                                ing.Quantity += quantityChange;
                                if (ing.Quantity < 0) ing.Quantity = 0;
                            }

                            // Tạo command cho từng nguyên liệu
                            foreach (var ing in ingredientsToUpdate)
                            {
                                var oldIng = oldIngredientsState.FirstOrDefault(o => o.Id == ing.Id);
                                if (oldIng != null)
                                {
                                    var command = new UpdateIngredientCommand(this, oldIng, ing);
                                    undoRedoService.ExecuteCommand(command);
                                }
                            }

                            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "BATCH_UPDATE_INGREDIENTS", "INGREDIENT", "",
                                $"Cập nhật {ingredientsToUpdate.Count} nguyên liệu, thay đổi {quantityChange}"));
                            SaveAllData();

                            EnhancedUI.DisplaySuccess("Cập nhật số lượng hàng loạt thành công!");
                        }
                        break;

                    case "3":
                        Console.Write("Nhập số lượng tối thiểu mới: ");
                        if (!decimal.TryParse(Console.ReadLine(), out decimal newMinQuantity))
                        {
                            EnhancedUI.DisplayError("Số lượng không hợp lệ!");
                            return;
                        }

                        ingredientsToUpdate = repository.Ingredients.Values.ToList();

                        // Lưu trạng thái cũ
                        foreach (var ing in ingredientsToUpdate)
                        {
                            oldIngredientsState.Add(new Ingredient(ing.Id, ing.Name, ing.Unit, ing.Quantity, ing.MinQuantity, ing.PricePerUnit));
                        }

                        Console.WriteLine($"Sẽ cập nhật {ingredientsToUpdate.Count} nguyên liệu");
                        if (EnhancedUI.Confirm("Xác nhận cập nhật?"))
                        {
                            foreach (var ing in ingredientsToUpdate)
                            {
                                ing.MinQuantity = newMinQuantity;
                            }

                            // Tạo command cho từng nguyên liệu
                            foreach (var ing in ingredientsToUpdate)
                            {
                                var oldIng = oldIngredientsState.FirstOrDefault(o => o.Id == ing.Id);
                                if (oldIng != null)
                                {
                                    var command = new UpdateIngredientCommand(this, oldIng, ing);
                                    undoRedoService.ExecuteCommand(command);
                                }
                            }

                            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "BATCH_UPDATE_INGREDIENTS", "INGREDIENT", "",
                                $"Cập nhật số lượng tối thiểu {newMinQuantity} cho {ingredientsToUpdate.Count} nguyên liệu"));
                            SaveAllData();

                            EnhancedUI.DisplaySuccess("Cập nhật số lượng tối thiểu hàng loạt thành công!");
                        }
                        break;

                    default:
                        EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Batch update ingredients failed", "IngredientManagement", ex);
                EnhancedUI.DisplayError($"Lỗi: {ex.Message}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void AddIngredientsFromFile()
        {
            EnhancedUI.DisplayHeader("THÊM NGUYÊN LIỆU TỪ FILE");

            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                if (!Directory.Exists(downloadPath))
                {
                    EnhancedUI.DisplayError("Thư mục Downloads không tồn tại!");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"Các file trong thư mục Downloads:");
                var files = Directory.GetFiles(downloadPath, "*.txt").Concat(
                           Directory.GetFiles(downloadPath, "*.csv")).ToArray();

                if (!files.Any())
                {
                    EnhancedUI.DisplayError("Không tìm thấy file .txt hoặc .csv trong thư mục Downloads!");
                    Console.ReadKey();
                    return;
                }

                for (int i = 0; i < files.Length; i++)
                {
                    Console.WriteLine($"{i + 1}. {Path.GetFileName(files[i])}");
                }

                Console.Write("Chọn file (số): ");
                if (int.TryParse(Console.ReadLine(), out int fileIndex) && fileIndex > 0 && fileIndex <= files.Length)
                {
                    string filePath = files[fileIndex - 1];
                    ImportIngredientsFromFile(filePath);
                }
                else
                {
                    EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to add ingredients from file", "IngredientManagement", ex);
                EnhancedUI.DisplayError($"Lỗi: {ex.Message}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ImportIngredientsFromFile(string filePath)
        {
            try
            {
                int successCount = 0;
                int errorCount = 0;
                List<Ingredient> importedIngredients = new List<Ingredient>();

                string[] lines = File.ReadAllLines(filePath);
                foreach (string line in lines.Skip(1)) // Bỏ qua header
                {
                    try
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length >= 6)
                        {
                            string id = parts[0].Trim();
                            string name = parts[1].Trim();
                            string unit = parts[2].Trim();
                            decimal quantity = decimal.Parse(parts[3].Trim());
                            decimal minQuantity = decimal.Parse(parts[4].Trim());
                            decimal price = decimal.Parse(parts[5].Trim());

                            if (!repository.Ingredients.ContainsKey(id))
                            {
                                var ingredient = new Ingredient(id, name, unit, quantity, minQuantity, price);
                                importedIngredients.Add(ingredient);
                                successCount++;
                            }
                            else
                            {
                                errorCount++;
                            }
                        }
                    }
                    catch
                    {
                        errorCount++;
                    }
                }

                if (importedIngredients.Any())
                {
                    // Tạo command cho từng nguyên liệu
                    foreach (var ingredient in importedIngredients)
                    {
                        var command = new AddIngredientCommand(this, ingredient);
                        undoRedoService.ExecuteCommand(command);
                    }

                    repository.AuditLogs.Add(new AuditLog(currentUser.Username, "IMPORT_INGREDIENTS", "INGREDIENT", "", $"Nhập từ file: {Path.GetFileName(filePath)}"));
                    SaveAllData();
                }

                EnhancedUI.DisplaySuccess($"Nhập dữ liệu thành công: {successCount} nguyên liệu");
                if (errorCount > 0)
                {
                    EnhancedUI.DisplayWarning($"Có {errorCount} nguyên liệu bị lỗi hoặc trùng mã");
                }

                Logger.Info($"Imported {successCount} ingredients from {filePath}", "IngredientManagement");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to import ingredients from {filePath}", "IngredientManagement", ex);
                EnhancedUI.DisplayError($"Lỗi khi đọc file: {ex.Message}");
            }
        }

        private void ShowInventoryMenu()
        {
            var menuOptions = new List<string>
    {
        "Nhập kho",
        "Xuất kho",
        "Kiểm kê kho",
        "Lịch sử nhập/xuất"
    };

            while (true)
            {
                int choice = EnhancedUI.ShowMenu("QUẢN LÝ KHO HÀNG", menuOptions);
                if (choice == 0) return;

                switch (choice)
                {
                    case 1: ImportInventory(); break;
                    case 2: ExportInventory(); break;
                    case 3: DisplayInventory(); break;
                    case 4: ShowInventoryHistory(); break;
                }
            }
        }

        private void ImportInventory(int page = 1, int pageSize = 10)
        {
            EnhancedUI.DisplayHeader("NHẬP KHO");

            var ingredients = repository.Ingredients.Values.ToList();
            int totalPages = (int)Math.Ceiling(ingredients.Count / (double)pageSize);

            if (ingredients.Count == 0)
            {
                EnhancedUI.DisplayInfo("Không có nguyên liệu nào trong hệ thống!");
                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
                return;
            }

            // Phân trang hiển thị
            var paged = ingredients.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            Console.WriteLine("══════════════════════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"{"MÃ",-8} {"TÊN NGUYÊN LIỆU",-25} {"ĐƠN VỊ",-10} {"SỐ LƯỢNG",-10} {"TỐI THIỂU",-10} {"GIÁ",-12}");
            Console.WriteLine("══════════════════════════════════════════════════════════════════════════════════════");

            foreach (var ing in paged)
            {
                Console.WriteLine($"{ing.Id,-8} {TruncateString(ing.Name, 25),-25} {ing.Unit,-10} {ing.Quantity,-10} {ing.MinQuantity,-10} {ing.PricePerUnit,12:N0}");
            }

            Console.WriteLine("══════════════════════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"Trang {page}/{totalPages} - Tổng cộng {ingredients.Count} nguyên liệu");
            Console.WriteLine("\n🔹 Nhập số trang để chuyển");
            Console.WriteLine("🔹 Nhập mã nguyên liệu để thực hiện nhập kho");
            Console.WriteLine("🔹 Nhập 0 để thoát");
            Console.Write("\nLựa chọn: ");

            string input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) { ImportInventory(page, pageSize); return; }

            // Thoát
            if (input == "0") return;

            // Chuyển trang
            if (int.TryParse(input, out int newPage))
            {
                if (newPage >= 1 && newPage <= totalPages)
                {
                    ImportInventory(newPage, pageSize);
                }
                else
                {
                    EnhancedUI.DisplayError("❌ Số trang không hợp lệ!");
                    Console.ReadKey();
                    ImportInventory(page, pageSize);
                }
                return;
            }

            // Thực hiện nhập kho
            string ingId = input;
            if (!repository.Ingredients.ContainsKey(ingId))
            {
                EnhancedUI.DisplayError($"❌ Nguyên liệu '{ingId}' không tồn tại!");
                Console.ReadKey();
                ImportInventory(page, pageSize);
                return;
            }

            var ingredient = repository.Ingredients[ingId];

            Console.Write($"Số lượng nhập cho {ingredient.Name}: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal quantity) || quantity <= 0)
            {
                EnhancedUI.DisplayError("Số lượng không hợp lệ!");
                Console.ReadKey();
                ImportInventory(page, pageSize);
                return;
            }

            var oldIngredient = new Ingredient(ingredient.Id, ingredient.Name, ingredient.Unit,
                ingredient.Quantity, ingredient.MinQuantity, ingredient.PricePerUnit);

            ingredient.Quantity += quantity;
            ingredient.LastUpdated = DateTime.Now;

            var command = new UpdateIngredientCommand(this, oldIngredient, ingredient);
            undoRedoService.ExecuteCommand(command);

            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "IMPORT_INVENTORY", "INGREDIENT", ingId, $"Nhập kho: +{quantity} {ingredient.Unit}"));
            SaveAllData();

            EnhancedUI.DisplaySuccess($"✅ Đã nhập kho {quantity} {ingredient.Unit} {ingredient.Name}!");
            Logger.Info($"Imported {quantity} {ingredient.Unit} of {ingredient.Name}", "Inventory");

            Console.WriteLine("\nNhấn phím bất kỳ để quay lại danh sách...");
            Console.ReadKey();
            ImportInventory(page, pageSize);
        }

        private void ExportInventory(int page = 1, int pageSize = 10)
        {
            EnhancedUI.DisplayHeader("XUẤT KHO");

            var ingredients = repository.Ingredients.Values.ToList();
            int totalPages = (int)Math.Ceiling(ingredients.Count / (double)pageSize);

            if (ingredients.Count == 0)
            {
                EnhancedUI.DisplayInfo("Không có nguyên liệu nào trong hệ thống!");
                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
                return;
            }

            // Phân trang hiển thị
            var paged = ingredients.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            Console.WriteLine("══════════════════════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"{"MÃ",-8} {"TÊN NGUYÊN LIỆU",-25} {"ĐƠN VỊ",-10} {"SỐ LƯỢNG",-10} {"TỐI THIỂU",-10} {"GIÁ",-12}");
            Console.WriteLine("══════════════════════════════════════════════════════════════════════════════════════");

            foreach (var ing in paged)
            {
                Console.WriteLine($"{ing.Id,-8} {TruncateString(ing.Name, 25),-25} {ing.Unit,-10} {ing.Quantity,-10} {ing.MinQuantity,-10} {ing.PricePerUnit,12:N0}");
            }

            Console.WriteLine("══════════════════════════════════════════════════════════════════════════════════════");
            Console.WriteLine($"Trang {page}/{totalPages} - Tổng cộng {ingredients.Count} nguyên liệu");
            Console.WriteLine("\n🔹 Nhập số trang để chuyển");
            Console.WriteLine("🔹 Nhập mã nguyên liệu để thực hiện xuất kho");
            Console.WriteLine("🔹 Nhập 0 để thoát");
            Console.Write("\nLựa chọn: ");

            string input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) { ExportInventory(page, pageSize); return; }

            // Thoát
            if (input == "0") return;

            // Chuyển trang
            if (int.TryParse(input, out int newPage))
            {
                if (newPage >= 1 && newPage <= totalPages)
                {
                    ExportInventory(newPage, pageSize);
                }
                else
                {
                    EnhancedUI.DisplayError("❌ Số trang không hợp lệ!");
                    Console.ReadKey();
                    ExportInventory(page, pageSize);
                }
                return;
            }

            // Thực hiện xuất kho
            string ingId = input;
            if (!repository.Ingredients.ContainsKey(ingId))
            {
                EnhancedUI.DisplayError($"❌ Nguyên liệu '{ingId}' không tồn tại!");
                Console.ReadKey();
                ExportInventory(page, pageSize);
                return;
            }

            var ingredient = repository.Ingredients[ingId];

            Console.Write($"Số lượng xuất cho {ingredient.Name}: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal quantity) || quantity <= 0)
            {
                EnhancedUI.DisplayError("Số lượng không hợp lệ!");
                Console.ReadKey();
                ExportInventory(page, pageSize);
                return;
            }

            if (ingredient.Quantity < quantity)
            {
                EnhancedUI.DisplayError("Số lượng xuất vượt quá tồn kho!");
                Console.ReadKey();
                ExportInventory(page, pageSize);
                return;
            }

            var oldIngredient = new Ingredient(ingredient.Id, ingredient.Name, ingredient.Unit,
                ingredient.Quantity, ingredient.MinQuantity, ingredient.PricePerUnit);

            ingredient.Quantity -= quantity;
            ingredient.LastUpdated = DateTime.Now;

            var command = new UpdateIngredientCommand(this, oldIngredient, ingredient);
            undoRedoService.ExecuteCommand(command);

            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "EXPORT_INVENTORY", "INGREDIENT", ingId, $"Xuất kho: -{quantity} {ingredient.Unit}"));
            SaveAllData();

            EnhancedUI.DisplaySuccess($"✅ Đã xuất kho {quantity} {ingredient.Unit} {ingredient.Name}!");
            Logger.Info($"Exported {quantity} {ingredient.Unit} of {ingredient.Name}", "Inventory");

            Console.WriteLine("\nNhấn phím bất kỳ để quay lại danh sách...");
            Console.ReadKey();
            ExportInventory(page, pageSize);
        }



        private void DisplayInventory()
        {
            int currentPage = 1;
            const int pageSize = 15;

            while (true)
            {
                Console.Clear();
                EnhancedUI.DisplayHeader("📦 KIỂM KÊ TỒN KHO NGUYÊN LIỆU");

                var ingredients = repository.Ingredients.Values.ToList();
                if (!ingredients.Any())
                {
                    EnhancedUI.DisplayInfo("Chưa có nguyên liệu nào trong kho!");
                    Console.WriteLine("\nNhấn phím bất kỳ để quay lại...");
                    Console.ReadKey();
                    return;
                }

                var lowStock = ingredients.Where(i => i.IsLowStock && i.Quantity > 0).ToList();
                var outOfStock = ingredients.Where(i => i.Quantity == 0).ToList();
                var sufficientStock = ingredients.Where(i => !i.IsLowStock && i.Quantity > 0).ToList();

                Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════════════════════════════════════");
                Console.WriteLine($"TỔNG QUAN KHO: {ingredients.Count} nguyên liệu | ✅ Đủ: {sufficientStock.Count} | ⚠️ Sắp hết: {lowStock.Count} | ❌ Hết hàng: {outOfStock.Count}");
                Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════════════════════════════════════");
                Console.WriteLine("{0,-8} {1,-25} {2,-10} {3,10} {4,12} {5,12} {6,14} {7,10}",
                    "MÃ", "TÊN", "ĐƠN VỊ", "TỒN", "TỐI THIỂU", "GIÁ (đ)", "TỔNG GIÁ TRỊ", "TRẠNG THÁI");
                Console.WriteLine("───────────────────────────────────────────────────────────────────────────────────────────────────────────────");

                var paged = ingredients.Skip((currentPage - 1) * pageSize).Take(pageSize);
                foreach (var ing in paged)
                {
                    string status = "✅ Đủ";
                    if (ing.Quantity == 0) status = "❌ Hết";
                    else if (ing.IsLowStock) status = "⚠️ Sắp hết";

                    decimal value = ing.Quantity * ing.PricePerUnit;
                    Console.WriteLine("{0,-8} {1,-25} {2,-10} {3,10:N1} {4,12:N1} {5,12:N0} {6,14:N0} {7,10}",
                        ing.Id,
                        TruncateString(ing.Name, 25),
                        ing.Unit,
                        ing.Quantity,
                        ing.MinQuantity,
                        ing.PricePerUnit,
                        value,
                        status);
                }

                Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════════════════════════════════════");
                decimal totalValue = ingredients.Sum(i => i.Quantity * i.PricePerUnit);
                decimal avgValue = ingredients.Count > 0 ? totalValue / ingredients.Count : 0;
                Console.WriteLine($"💰 Tổng giá trị tồn kho: {totalValue:N0} đ | Giá trị TB/nguyên liệu: {avgValue:N0} đ");
                Console.WriteLine($"📄 Trang {currentPage}/{Math.Ceiling(ingredients.Count / (double)pageSize)}");
                Console.WriteLine("───────────────────────────────────────────────────────────────────────────────────────────────────────────────");

                if (outOfStock.Any())
                {
                    Console.WriteLine("\n🚨 NGUYÊN LIỆU HẾT HÀNG (TỐI ĐA 5):");
                    foreach (var i in outOfStock.Take(5))
                        Console.WriteLine($" - {i.Name} ({i.Id})");
                }

                if (lowStock.Any())
                {
                    Console.WriteLine("\n⚠️ NGUYÊN LIỆU SẮP HẾT (TỐI ĐA 5):");
                    foreach (var i in lowStock.Take(5))
                        Console.WriteLine($" - {i.Name} ({i.Quantity}/{i.MinQuantity} {i.Unit})");
                }

                Console.WriteLine("\nNhập số trang để chuyển hoặc ENTER để thoát.");
                Console.Write("👉 Trang: ");
                string input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input)) break;

                int pageNum;
                if (int.TryParse(input, out pageNum))
                {
                    int totalPages = (int)Math.Ceiling(ingredients.Count / (double)pageSize);
                    if (pageNum >= 1 && pageNum <= totalPages)
                        currentPage = pageNum;
                    else
                        EnhancedUI.DisplayError($"❌ Trang không hợp lệ (1 - {totalPages})");
                }
            }

            Logger.Info("Inventory display completed", "Inventory");
        }


        private void ShowInventoryHistory()
        {
            int currentPage = 1;
            const int pageSize = 15;

            while (true)
            {
                Console.Clear();
                EnhancedUI.DisplayHeader("📜 LỊCH SỬ NHẬP/XUẤT KHO");

                var inventoryLogs = repository.AuditLogs
                    .Where(log => log.Action == "IMPORT_INVENTORY" || log.Action == "EXPORT_INVENTORY")
                    .OrderByDescending(log => log.Timestamp)
                    .ToList();

                if (!inventoryLogs.Any())
                {
                    EnhancedUI.DisplayInfo("❌ Chưa có lịch sử nhập/xuất kho");
                    Console.WriteLine("\nNhấn phím bất kỳ để quay lại...");
                    Console.ReadKey();
                    return;
                }

                int totalPages = (int)Math.Ceiling(inventoryLogs.Count / (double)pageSize);
                var pageData = inventoryLogs.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();

                Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║ {0,-17} │ {1,-10} │ {2,-25} │ {3,-12} │ {4,-15} ║",
                    "Thời gian", "Loại", "Nguyên liệu", "Số lượng", "Người thực hiện");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════════════════════╣");

                int totalImport = 0;
                int totalExport = 0;

                foreach (var log in pageData)
                {
                    string actionType = log.Action == "IMPORT_INVENTORY" ? "📥 Nhập" : "📤 Xuất";
                    string ingredientName = repository.Ingredients.ContainsKey(log.EntityId)
                        ? repository.Ingredients[log.EntityId].Name
                        : "Unknown";

                    // --- Lấy số lượng từ log.Details ---
                    int quantity = 0;
                    if (!string.IsNullOrWhiteSpace(log.Details))
                    {
                        var digits = new string(log.Details.Where(char.IsDigit).ToArray());
                        int.TryParse(digits, out quantity);
                    }

                    if (log.Action == "IMPORT_INVENTORY") totalImport += quantity;
                    else totalExport += quantity;

                    // --- Rút gọn chuỗi nếu dài ---
                    string shortName = ingredientName.Length <= 25 ? ingredientName : ingredientName.Substring(0, 22) + "...";
                    string userName = string.IsNullOrEmpty(log.Username) ? "Không rõ" : log.Username;
                    Console.WriteLine("║ {0,-17} │ {1,-10} │ {2,-25} │ {3,-12} │ {4,-15} ║",
                        log.Timestamp.ToString("dd/MM/yyyy HH:mm"),
                        actionType,
                        shortName,
                        quantity.ToString("N0"),
                        userName);
                }

                Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════════════════════╝");

                Console.WriteLine($"\n📊 Thống kê trang {currentPage}/{totalPages}:");
                Console.WriteLine($"   📥 Tổng nhập: {totalImport:N0}");
                Console.WriteLine($"   📤 Tổng xuất: {totalExport:N0}");
                Console.WriteLine($"   ⚖️  Chênh lệch: {(totalImport - totalExport):N0}");

                Console.WriteLine("\n────────────────────────────────────────────────────────────");
                Console.Write("👉 Nhập số trang để chuyển (ENTER để thoát): ");
                string input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                    break;

                if (int.TryParse(input, out int pageNum))
                {
                    if (pageNum >= 1 && pageNum <= totalPages)
                        currentPage = pageNum;
                    else
                    {
                        EnhancedUI.DisplayError($"⚠️ Trang không hợp lệ (1 - {totalPages})");
                        Console.ReadKey();
                    }
                }
            }

            Logger.Info("Inventory history displayed", "InventoryHistory");
        }


        private void ShowIngredientStatistics()
        {
            EnhancedUI.DisplayHeader("THỐNG KÊ NGUYÊN LIỆU");

            var ingredientUsage = new Dictionary<string, int>();
            foreach (var dish in repository.Dishes.Values)
            {
                foreach (var ing in dish.Ingredients)
                {
                    if (ingredientUsage.ContainsKey(ing.Key)) ingredientUsage[ing.Key]++;
                    else ingredientUsage[ing.Key] = 1;
                }
            }

            var topUsedIngredients = ingredientUsage
                .OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .ToList();

            Console.WriteLine("🔝 TOP NGUYÊN LIỆU ĐƯỢC SỬ DỤNG NHIỀU NHẤT:");
            Console.WriteLine("─────────────────────────────────────────────");
            Console.WriteLine($"{"Tên nguyên liệu",-25} {"Số món sử dụng",-15}");
            Console.WriteLine("─────────────────────────────────────────────");

            foreach (var kvp in topUsedIngredients)
            {
                string ingId = kvp.Key;
                int count = kvp.Value;
                if (repository.Ingredients.ContainsKey(ingId))
                {
                    var ing = repository.Ingredients[ingId];
                    Console.WriteLine($"{TruncateString(ing.Name, 25),-25} {count,-15}");
                }
            }

            var valuableIngredients = repository.Ingredients.Values
                .OrderByDescending(ing => ing.Quantity * ing.PricePerUnit)
                .Take(5)
                .ToList();

            Console.WriteLine("\n💰 TOP NGUYÊN LIỆU CÓ GIÁ TRỊ TỒN KHO CAO:");
            Console.WriteLine("─────────────────────────────────────────────");
            Console.WriteLine($"{"Tên nguyên liệu",-25} {"Giá trị tồn kho",-15} {"Số lượng",-10}");
            Console.WriteLine("─────────────────────────────────────────────");
            foreach (var ing in valuableIngredients)
            {
                decimal value = ing.Quantity * ing.PricePerUnit;
                Console.WriteLine($"{TruncateString(ing.Name, 25),-25} {value,15:N0} {ing.Quantity,10}");
            }

            int lowStockCount = repository.Ingredients.Values.Count(ing => ing.IsLowStock);
            int outOfStockCount = repository.Ingredients.Values.Count(ing => ing.Quantity == 0);
            int totalCount = repository.Ingredients.Count;
            decimal stockGoodPercent = totalCount == 0 ? 0 : ((decimal)(totalCount - lowStockCount - outOfStockCount) / totalCount * 100);

            Console.WriteLine("\n📊 TỔNG HỢP CẢNH BÁO:");
            Console.WriteLine($"- Nguyên liệu sắp hết: {lowStockCount}");
            Console.WriteLine($"- Nguyên liệu đã hết: {outOfStockCount}");
            Console.WriteLine($"- Tỷ lệ stock tốt: {stockGoodPercent:F1}%");

            Logger.Info("Ingredient statistics generated", "IngredientManagement");

            // Hỏi người dùng có muốn xuất CSV
            Console.Write("\nBạn có muốn xuất thống kê ra file CSV không? (y/n): ");
            string exportChoice = Console.ReadLine().Trim().ToLower();
            if (exportChoice == "y")
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                string filePath = Path.Combine(downloadPath, "Ingredient_Statistics.csv");

                using (var writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("Top nguyên liệu được sử dụng nhiều nhất:");
                    writer.WriteLine("Tên nguyên liệu,Số món sử dụng");
                    foreach (var kvp in topUsedIngredients)
                    {
                        if (repository.Ingredients.ContainsKey(kvp.Key))
                        {
                            var ing = repository.Ingredients[kvp.Key];
                            writer.WriteLine($"{ing.Name},{kvp.Value}");
                        }
                    }

                    writer.WriteLine();
                    writer.WriteLine("Top nguyên liệu có giá trị tồn kho cao:");
                    writer.WriteLine("Tên nguyên liệu,Giá trị tồn kho,Số lượng");
                    foreach (var ing in valuableIngredients)
                    {
                        decimal value = ing.Quantity * ing.PricePerUnit;
                        writer.WriteLine($"{ing.Name},{value:N0},{ing.Quantity}");
                    }

                    writer.WriteLine();
                    writer.WriteLine("Cảnh báo kho:");
                    writer.WriteLine($"Nguyên liệu sắp hết,{lowStockCount}");
                    writer.WriteLine($"Nguyên liệu đã hết,{outOfStockCount}");
                    writer.WriteLine($"Tỷ lệ stock tốt,{stockGoodPercent:F1}%");
                }

                Console.WriteLine($"\n✅ Đã xuất thống kê ra file CSV tại: {filePath}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }


        // ==================== COMBO MANAGEMENT METHODS ====================
        private void ShowComboManagementMenu()
        {
            var menuOptions = new List<string>
    {
        "Xem danh sách combo",
        "Tạo combo mới",
        "Cập nhật combo",
        "Xóa combo",
        "Tự động sinh combo",
        "Thống kê combo bán chạy",
        "Xem chi tiết combo",
        "Liệt kê combo thực đơn tiệc"
    };

            while (true)
            {
                int choice = EnhancedUI.ShowMenu("QUẢN LÝ COMBO & KHUYẾN MÃI", menuOptions);
                if (choice == 0) return;

                switch (choice)
                {
                    case 1: DisplayCombos(); break;
                    case 2: CreateCombo(); break;
                    case 3: UpdateCombo(); break;
                    case 4: DeleteCombo(); break;
                    case 5: AutoGenerateCombo(); break;
                    case 6: ShowComboSalesReport(); break;
                    case 7: ShowComboDetail(); break;
                    case 8: GeneratePartyMenuCombos(); break;
                }
            }
        }

        private void DisplayCombos(int page = 1, int pageSize = 20)
        {
            EnhancedUI.DisplayHeader("DANH SÁCH COMBO");

            var comboList = repository.Combos.Values.Where(c => c.IsActive).ToList();
            int totalPages = (int)Math.Ceiling(comboList.Count / (double)pageSize);

            if (comboList.Count == 0)
            {
                EnhancedUI.DisplayInfo("Chưa có combo nào trong hệ thống!");
                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
                return;
            }

            var pagedCombos = comboList.Skip((page - 1) * pageSize).Take(pageSize);

            // Tạm thời tắt log console
            var originalOut = Console.Out;
            Console.SetOut(TextWriter.Null);
            foreach (var combo in pagedCombos)
            {
                combo.CalculateOriginalPrice(repository.Dishes);
                combo.CalculateCost(repository.Dishes);
            }
            Console.SetOut(originalOut); // khôi phục

            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                                  DANH SÁCH COMBO                               ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-8} {1,-25} {2,-8} {3,-12} {4,-12} {5,-8} ║",
                "Mã", "Tên combo", "Số món", "Giá gốc", "Giá KM", "Giảm giá");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

            foreach (var combo in pagedCombos)
            {
                Console.WriteLine("║ {0,-8} {1,-25} {2,-8} {3,-12} {4,-12} {5,-8} ║",
                    combo.Id,
                    TruncateString(combo.Name, 25),
                    combo.DishIds.Count,
                    $"{combo.OriginalPrice:N0}đ",
                    $"{combo.FinalPrice:N0}đ",
                    $"{combo.DiscountPercent}%");
            }

            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine($"\nTrang {page}/{totalPages} - Tổng cộng: {comboList.Count} combo");

            Console.Write("Nhập số trang để chuyển hoặc 0 để thoát: ");
            string choice = Console.ReadLine()?.Trim();
            if (int.TryParse(choice, out int pageNum))
            {
                if (pageNum == 0) return;
                if (pageNum >= 1 && pageNum <= totalPages)
                    DisplayCombos(pageNum, pageSize);
                else
                {
                    EnhancedUI.DisplayError("Số trang không hợp lệ!");
                    Console.ReadKey();
                    DisplayCombos(page, pageSize);
                }
            }
            else
            {
                EnhancedUI.DisplayError("Nhập không hợp lệ!");
                Console.ReadKey();
                DisplayCombos(page, pageSize);
            }
        }


        private void CreateCombo()
        {
            EnhancedUI.DisplayHeader("🍱 TẠO COMBO MỚI");

            try
            {
                Console.Write("Mã combo: ");
                string id = Console.ReadLine();

                if (repository.Combos.ContainsKey(id))
                {
                    EnhancedUI.DisplayError("❌ Mã combo đã tồn tại!");
                    return;
                }

                Console.Write("Tên combo: ");
                string name = Console.ReadLine();

                Console.Write("Mô tả: ");
                string description = Console.ReadLine();

                Console.Write("Phần trăm giảm giá (%): ");
                if (!decimal.TryParse(Console.ReadLine(), out decimal discount) || discount < 0 || discount > 100)
                {
                    EnhancedUI.DisplayError("❌ Phần trăm giảm giá không hợp lệ!");
                    return;
                }

                var combo = new Combo(id, name, description, discount);

                // 🔸 Bắt đầu chọn món ăn có phân trang
                int page = 1;
                const int pageSize = 10;

                while (true)
                {
                    Console.Clear();
                    EnhancedUI.DisplayHeader($"🍽 DANH SÁCH MÓN ĂN — Trang {page}");

                    var dishes = repository.Dishes.Values
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

                    if (dishes.Count == 0)
                    {
                        EnhancedUI.DisplayWarning("Không có món ăn nào để hiển thị.");
                        break;
                    }

                    // 🧾 Hiển thị bảng món ăn
                    Console.WriteLine("╔════════════════════════════════════════════════════════════════════════╗");
                    Console.WriteLine("║ {0,-8} │ {1,-25} │ {2,-10} │ {3,-10} ║", "Mã", "Tên món", "Giá", "Trạng thái");
                    Console.WriteLine("╠════════════════════════════════════════════════════════════════════════╣");

                    foreach (var dish in dishes)
                    {
                        string status = dish.IsAvailable ? "✅ Có" : "❌ Hết";
                        Console.WriteLine("║ {0,-8} │ {1,-25} │ {2,-10:N0} │ {3,-10} ║",
                            dish.Id, TruncateString(dish.Name, 25), dish.Price, status);
                    }

                    Console.WriteLine("╚════════════════════════════════════════════════════════════════════════╝");

                    Console.WriteLine("\n👉 Nhập mã món để thêm vào combo");
                    Console.WriteLine("👉 Nhập số trang để chuyển (vd: 2)");
                    Console.WriteLine("👉 Nhập trống để kết thúc chọn món");
                    Console.Write("\nLựa chọn của bạn: ");
                    string input = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(input))
                        break;

                    // Nếu nhập số → chuyển trang
                    if (int.TryParse(input, out int newPage))
                    {
                        int totalPages = (int)Math.Ceiling(repository.Dishes.Count / (double)pageSize);
                        if (newPage >= 1 && newPage <= totalPages)
                        {
                            page = newPage;
                            continue;
                        }
                        else
                        {
                            EnhancedUI.DisplayWarning($"⚠ Trang không hợp lệ! (1 - {totalPages})");
                            continue;
                        }
                    }

                    // Nếu nhập mã món → thêm món vào combo
                    if (!repository.Dishes.ContainsKey(input))
                    {
                        EnhancedUI.DisplayError("❌ Món ăn không tồn tại!");
                        continue;
                    }

                    if (combo.DishIds.Contains(input))
                    {
                        EnhancedUI.DisplayWarning("⚠ Món ăn đã có trong combo!");
                        continue;
                    }

                    combo.DishIds.Add(input);
                    EnhancedUI.DisplaySuccess($"✅ Đã thêm '{repository.Dishes[input].Name}' vào combo!");

                    if (!EnhancedUI.Confirm("Tiếp tục thêm món?"))
                        break;
                }

                if (combo.DishIds.Count == 0)
                {
                    EnhancedUI.DisplayError("❌ Combo phải có ít nhất 1 món!");
                    return;
                }

                // 🔹 Tính giá combo
                combo.CalculateOriginalPrice(repository.Dishes);
                combo.CalculateCost(repository.Dishes);

                repository.Combos[id] = combo;
                repository.AuditLogs.Add(new AuditLog(currentUser.Username, "CREATE_COMBO", "COMBO", id, $"Tạo combo: {name}"));
                SaveAllData();

                // ✅ Thông tin tổng kết
                EnhancedUI.DisplaySuccess($"🎉 Tạo combo '{name}' thành công!");
                Console.WriteLine($"- Số món: {combo.DishIds.Count}");
                Console.WriteLine($"- Giá gốc: {combo.OriginalPrice:N0}đ");
                Console.WriteLine($"- Giá khuyến mãi: {combo.FinalPrice:N0}đ");
                Console.WriteLine($"- Tiết kiệm: {combo.OriginalPrice - combo.FinalPrice:N0}đ");
                Console.WriteLine($"- Lợi nhuận: {combo.ProfitMargin:F1}%");

                Logger.Info($"Combo {id} created successfully", "ComboManagement");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create combo", "ComboManagement", ex);
                EnhancedUI.DisplayError($"❌ Lỗi: {ex.Message}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }


        private void UpdateCombo()
        {
            EnhancedUI.DisplayHeader("CẬP NHẬT COMBO");

            var activeCombos = repository.Combos.Values.Where(c => c.IsActive).ToList();
            if (!activeCombos.Any())
            {
                EnhancedUI.DisplayInfo("Chưa có combo nào!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("DANH SÁCH COMBO:");
            foreach (var cb in activeCombos.Take(10)) // 🔹 đổi tên biến combo -> cb
            {
                cb.CalculateOriginalPrice(repository.Dishes);
                Console.WriteLine($"{cb.Id} - {cb.Name} - {cb.FinalPrice:N0}đ");
            }

            Console.Write("\nNhập mã combo cần cập nhật: ");
            string comboId = Console.ReadLine();

            if (!repository.Combos.ContainsKey(comboId) || !repository.Combos[comboId].IsActive)
            {
                EnhancedUI.DisplayError("Combo không tồn tại!");
                Console.ReadKey();
                return;
            }

            var combo = repository.Combos[comboId]; // 🔹 giờ không trùng tên nữa

            try
            {
                Console.WriteLine($"\nCập nhật combo: {combo.Name}");
                Console.WriteLine("(Để trống nếu giữ nguyên)");

                Console.Write($"Tên combo ({combo.Name}): ");
                string name = Console.ReadLine();
                if (!string.IsNullOrEmpty(name)) combo.Name = name;

                Console.Write($"Mô tả ({combo.Description}): ");
                string description = Console.ReadLine();
                if (!string.IsNullOrEmpty(description)) combo.Description = description;

                Console.Write($"Phần trăm giảm giá ({combo.DiscountPercent}%): ");
                string discountStr = Console.ReadLine();
                if (!string.IsNullOrEmpty(discountStr) && decimal.TryParse(discountStr, out decimal discount))
                {
                    combo.DiscountPercent = discount;
                }

                if (EnhancedUI.Confirm("Quản lý món trong combo?"))
                {
                    ManageComboDishes(combo);
                }

                combo.CalculateOriginalPrice(repository.Dishes);
                combo.CalculateCost(repository.Dishes);

                repository.AuditLogs.Add(new AuditLog(currentUser.Username, "UPDATE_COMBO", "COMBO", comboId, $"Cập nhật combo: {combo.Name}"));
                SaveAllData();

                EnhancedUI.DisplaySuccess("Cập nhật combo thành công!");
                Logger.Info($"Combo {comboId} updated", "ComboManagement");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to update combo {comboId}", "ComboManagement", ex);
                EnhancedUI.DisplayError($"Lỗi: {ex.Message}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }


        private void ManageComboDishes(Combo combo)
        {
            int pageSize = 10;

            while (true)
            {
                Console.Clear();
                EnhancedUI.DisplayHeader($"🍱 QUẢN LÝ MÓN TRONG COMBO: {combo.Name}");

                // 🧾 Hiển thị món trong combo
                if (combo.DishIds.Any())
                {
                    Console.WriteLine("╔════════════════════════════════════════════════════════════════════════╗");
                    Console.WriteLine("║ {0,-8} │ {1,-25} │ {2,-10} │ {3,-10} ║", "Mã", "Tên món", "Giá", "Trạng thái");
                    Console.WriteLine("╠════════════════════════════════════════════════════════════════════════╣");

                    foreach (var dishId in combo.DishIds)
                    {
                        if (repository.Dishes.ContainsKey(dishId))
                        {
                            var dish = repository.Dishes[dishId];
                            string status = dish.IsAvailable ? "✅ Có" : "❌ Hết";
                            Console.WriteLine("║ {0,-8} │ {1,-25} │ {2,-10:N0} │ {3,-10} ║",
                                dish.Id, TruncateString(dish.Name, 25), dish.Price, status);
                        }
                    }

                    Console.WriteLine("╚════════════════════════════════════════════════════════════════════════╝");
                }
                else
                {
                    EnhancedUI.DisplayWarning("⚠ Combo hiện chưa có món nào!");
                }

                Console.WriteLine("\n1️⃣ Thêm món vào combo");
                Console.WriteLine("2️⃣ Xóa món khỏi combo");
                Console.WriteLine("0️⃣ Quay lại");
                Console.Write("\n👉 Chọn: ");

                string choice = Console.ReadLine();
                switch (choice)
                {
                    case "1":
                        int page = 1;
                        while (true)
                        {
                            Console.Clear();
                            EnhancedUI.DisplayHeader($"🍽 DANH SÁCH MÓN ĂN — Trang {page}");

                            var dishes = repository.Dishes.Values
                                .Skip((page - 1) * pageSize)
                                .Take(pageSize)
                                .ToList();

                            if (dishes.Count == 0)
                            {
                                EnhancedUI.DisplayWarning("⚠ Không có món ăn nào để hiển thị.");
                                break;
                            }

                            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════╗");
                            Console.WriteLine("║ {0,-8} │ {1,-25} │ {2,-10} │ {3,-10} ║", "Mã", "Tên món", "Giá", "Trạng thái");
                            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════╣");

                            foreach (var dish in dishes)
                            {
                                string status = dish.IsAvailable ? "✅ Có" : "❌ Hết";
                                Console.WriteLine("║ {0,-8} │ {1,-25} │ {2,-10:N0} │ {3,-10} ║",
                                    dish.Id, TruncateString(dish.Name, 25), dish.Price, status);
                            }

                            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════╝");
                            Console.WriteLine("\n👉 Nhập mã món để thêm vào combo");
                            Console.WriteLine("👉 Nhập số trang để chuyển (vd: 2)");
                            Console.WriteLine("👉 Nhập trống để quay lại");

                            Console.Write("\nLựa chọn của bạn: ");
                            string input = Console.ReadLine();

                            if (string.IsNullOrWhiteSpace(input))
                                break;

                            // Nếu nhập số -> chuyển trang
                            if (int.TryParse(input, out int newPage))
                            {
                                int totalPages = (int)Math.Ceiling(repository.Dishes.Count / (double)pageSize);
                                if (newPage >= 1 && newPage <= totalPages)
                                {
                                    page = newPage;
                                    continue;
                                }
                                else
                                {
                                    EnhancedUI.DisplayWarning($"⚠ Trang không hợp lệ! (1 - {totalPages})");
                                    continue;
                                }
                            }

                            // Nếu nhập mã món
                            if (!repository.Dishes.ContainsKey(input))
                            {
                                EnhancedUI.DisplayError("❌ Món ăn không tồn tại!");
                                continue;
                            }

                            if (combo.DishIds.Contains(input))
                            {
                                EnhancedUI.DisplayWarning("⚠ Món ăn đã có trong combo!");
                                continue;
                            }

                            combo.DishIds.Add(input);
                            EnhancedUI.DisplaySuccess($"✅ Đã thêm '{repository.Dishes[input].Name}' vào combo!");

                            if (!EnhancedUI.Confirm("Tiếp tục thêm món?"))
                                break;
                        }
                        break;

                    case "2":
                        if (!combo.DishIds.Any())
                        {
                            EnhancedUI.DisplayWarning("⚠ Combo chưa có món để xóa!");
                            break;
                        }

                        Console.Write("\nNhập mã món cần xóa khỏi combo: ");
                        string dishIdToRemove = Console.ReadLine();

                        if (combo.DishIds.Contains(dishIdToRemove))
                        {
                            combo.DishIds.Remove(dishIdToRemove);
                            EnhancedUI.DisplaySuccess("🗑 Đã xóa món khỏi combo!");
                        }
                        else
                        {
                            EnhancedUI.DisplayError("❌ Món ăn không tồn tại trong combo!");
                        }
                        break;

                    case "0":
                        return;

                    default:
                        EnhancedUI.DisplayError("❌ Lựa chọn không hợp lệ!");
                        break;
                }
            }
        }


        private void DeleteCombo()
        {
            EnhancedUI.DisplayHeader("XÓA COMBO");

            var activeCombos = repository.Combos.Values
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.SalesCount)
                .ToList();

            if (!activeCombos.Any())
            {
                EnhancedUI.DisplayInfo("Chưa có combo nào!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("DANH SÁCH COMBO:");
            foreach (var cb in activeCombos.Take(10)) // 🔹 đổi combo → cb cho rõ
            {
                Console.WriteLine($"{cb.Id} - {cb.Name} - {cb.SalesCount} lượt bán");
            }

            Console.Write("\nNhập mã combo cần xóa: ");
            string comboId = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(comboId) ||
                !repository.Combos.ContainsKey(comboId) ||
                !repository.Combos[comboId].IsActive)
            {
                EnhancedUI.DisplayError("Combo không tồn tại hoặc đã bị xóa!");
                Console.ReadKey();
                return;
            }

            var combo = repository.Combos[comboId];

            Console.WriteLine("\nThông tin combo:");
            Console.WriteLine($"- Tên: {combo.Name}");
            Console.WriteLine($"- Số món: {combo.DishIds.Count}");
            Console.WriteLine($"- Đã bán: {combo.SalesCount} lượt");

            if (EnhancedUI.Confirm($"Xác nhận xóa combo '{combo.Name}'?"))
            {
                combo.IsActive = false; // 🔹 chỉ đánh dấu, không xóa dữ liệu
                repository.AuditLogs.Add(new AuditLog(currentUser.Username, "DELETE_COMBO", "COMBO", comboId, $"Xóa combo: {combo.Name}"));
                SaveAllData();

                EnhancedUI.DisplaySuccess("Xóa combo thành công!");
                Logger.Info($"Combo {comboId} deleted", "ComboManagement");
            }
            else
            {
                EnhancedUI.DisplayInfo("Hủy thao tác xóa combo.");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void AutoGenerateCombo()
        {
            EnhancedUI.DisplayHeader("TỰ ĐỘNG SINH COMBO");

            Console.WriteLine("1. Combo theo nhóm món");
            Console.WriteLine("2. Combo khuyến mãi theo nguyên liệu");
            Console.WriteLine("3. Combo ngẫu nhiên");
            Console.Write("Chọn loại combo: ");

            string choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    GenerateCategoryCombo();
                    break;
                case "2":
                    GeneratePromotionCombo();
                    break;
                case "3":
                    GenerateRandomCombo();
                    break;
                default:
                    EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                    break;
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }


        private void GenerateCategoryCombo()
        {
            Console.Write("Nhập nhóm món: ");
            string category = Console.ReadLine();

            var categoryDishes = repository.Dishes.Values
                .Where(d => d.Category.ToLower().Contains(category.ToLower()) && d.IsAvailable && CheckDishIngredients(d))
                .Take(4)
                .ToList();

            if (categoryDishes.Count < 2)
            {
                EnhancedUI.DisplayError("Không đủ món để tạo combo!");
                Console.ReadKey();
                return;
            }

            string comboId = "AUTO_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            var combo = new Combo(comboId, $"Combo {category}", $"Combo tự động sinh cho nhóm {category}", 15);

            foreach (var dish in categoryDishes)
            {
                combo.DishIds.Add(dish.Id);
            }

            combo.CalculateOriginalPrice(repository.Dishes);
            combo.CalculateCost(repository.Dishes);
            repository.Combos[comboId] = combo;

            EnhancedUI.DisplaySuccess($"Đã tạo combo {comboId}!");
            Console.WriteLine($"- Số món: {combo.DishIds.Count}");
            Console.WriteLine($"- Giá gốc: {combo.OriginalPrice:N0}đ");
            Console.WriteLine($"- Giá KM: {combo.FinalPrice:N0}đ");

            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "AUTO_GENERATE_COMBO", "COMBO", comboId, $"Combo nhóm {category}"));
            SaveAllData();

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void GeneratePromotionCombo()
        {
            // Tìm các món có nguyên liệu sắp hết để khuyến mãi
            var promotionDishes = repository.Dishes.Values
                .Where(d => d.IsAvailable && d.Ingredients.Any(ing =>
                    repository.Ingredients.ContainsKey(ing.Key) && repository.Ingredients[ing.Key].IsLowStock))
                .Take(3)
                .ToList();

            if (promotionDishes.Count < 2)
            {
                EnhancedUI.DisplayError("Không đủ món để tạo combo khuyến mãi!");
                Console.ReadKey();
                return;
            }

            string comboId = "PROMO_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            var combo = new Combo(comboId, "Combo Khuyến Mãi", "Combo khuyến mãi nguyên liệu sắp hết", 20);

            foreach (var dish in promotionDishes)
            {
                combo.DishIds.Add(dish.Id);
            }

            combo.CalculateOriginalPrice(repository.Dishes);
            combo.CalculateCost(repository.Dishes);
            repository.Combos[comboId] = combo;

            EnhancedUI.DisplaySuccess($"Đã tạo combo khuyến mãi {comboId}!");
            Console.WriteLine($"- Số món: {combo.DishIds.Count}");
            Console.WriteLine($"- Giá gốc: {combo.OriginalPrice:N0}đ");
            Console.WriteLine($"- Giá KM: {combo.FinalPrice:N0}đ");

            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "AUTO_GENERATE_PROMO_COMBO", "COMBO", comboId, "Combo khuyến mãi"));
            SaveAllData();

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void GenerateRandomCombo()
        {
            var availableDishes = repository.Dishes.Values
                .Where(d => d.IsAvailable && CheckDishIngredients(d))
                .ToList();

            if (availableDishes.Count < 3)
            {
                EnhancedUI.DisplayError("Không đủ món để tạo combo!");
                Console.ReadKey();
                return;
            }

            var random = new Random();
            var selectedDishes = availableDishes.OrderBy(x => random.Next()).Take(3).ToList();

            string comboId = "RANDOM_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            var combo = new Combo(comboId, "Combo Ngẫu Nhiên", "Combo được tạo ngẫu nhiên từ menu", 10);

            foreach (var dish in selectedDishes)
            {
                combo.DishIds.Add(dish.Id);
            }

            combo.CalculateOriginalPrice(repository.Dishes);
            combo.CalculateCost(repository.Dishes);
            repository.Combos[comboId] = combo;

            EnhancedUI.DisplaySuccess($"Đã tạo combo ngẫu nhiên {comboId}!");
            Console.WriteLine($"- Số món: {combo.DishIds.Count}");
            Console.WriteLine($"- Giá gốc: {combo.OriginalPrice:N0}đ");
            Console.WriteLine($"- Giá KM: {combo.FinalPrice:N0}đ");

            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "AUTO_GENERATE_RANDOM_COMBO", "COMBO", comboId, "Combo ngẫu nhiên"));
            SaveAllData();

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ShowComboSalesReport()
        {
            EnhancedUI.DisplayHeader("THỐNG KÊ COMBO BÁN CHẠY");

            var topCombos = repository.Combos.Values
                .Where(c => c.IsActive && c.SalesCount > 0)
                .OrderByDescending(c => c.SalesCount)
                .Take(10)
                .ToList();

            if (!topCombos.Any())
            {
                EnhancedUI.DisplayInfo("Chưa có combo nào được bán!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                           TOP COMBO BÁN CHẠY                                 ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-3} {1,-25} {2,-10} {3,-15} {4,-15} ║",
                "STT", "Tên combo", "Số lượt", "Doanh thu", "Lợi nhuận");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

            for (int i = 0; i < topCombos.Count; i++)
            {
                var combo = topCombos[i];
                combo.CalculateOriginalPrice(repository.Dishes);
                combo.CalculateCost(repository.Dishes);

                decimal revenue = combo.FinalPrice * combo.SalesCount;
                decimal profit = (combo.FinalPrice - combo.Cost) * combo.SalesCount;

                Console.WriteLine("║ {0,-3} {1,-25} {2,-10} {3,-15} {4,-15} ║",
                    i + 1,
                    TruncateString(combo.Name, 25),
                    combo.SalesCount,
                    $"{revenue:N0}đ",
                    $"{profit:N0}đ");
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");

            // Xuất báo cáo
            if (EnhancedUI.Confirm("Xuất báo cáo combo ra file?"))
            {
                ExportComboReport(topCombos);
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ExportComboReport(List<Combo> topCombos)
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"BaoCaoCombo_{DateTime.Now:yyyyMMddHHmmss}.txt";
                string filePath = Path.Combine(downloadPath, fileName);

                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("BÁO CÁO COMBO BÁN CHẠY");
                    writer.WriteLine($"Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                    writer.WriteLine("==========================================");
                    writer.WriteLine();

                    foreach (var combo in topCombos)
                    {
                        combo.CalculateOriginalPrice(repository.Dishes);
                        combo.CalculateCost(repository.Dishes);

                        decimal revenue = combo.FinalPrice * combo.SalesCount;
                        decimal profit = (combo.FinalPrice - combo.Cost) * combo.SalesCount;

                        writer.WriteLine($"{combo.Name}:");
                        writer.WriteLine($"  - Số lượt bán: {combo.SalesCount}");
                        writer.WriteLine($"  - Doanh thu: {revenue:N0}đ");
                        writer.WriteLine($"  - Lợi nhuận: {profit:N0}đ");
                        writer.WriteLine($"  - Giá bán: {combo.FinalPrice:N0}đ");
                        writer.WriteLine($"  - Chiết khấu: {combo.DiscountPercent}%");
                        writer.WriteLine();
                    }
                }

                EnhancedUI.DisplaySuccess($"Đã xuất báo cáo: {fileName}");
                Logger.Info($"Combo report exported: {fileName}", "ComboManagement");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to export combo report", "ComboManagement", ex);
                EnhancedUI.DisplayError($"Lỗi xuất báo cáo: {ex.Message}");
            }
        }

        private void ShowComboDetail()
        {
            EnhancedUI.DisplayHeader("CHI TIẾT COMBO");

            var activeCombos = repository.Combos.Values.Where(c => c.IsActive).ToList();
            if (!activeCombos.Any())
            {
                EnhancedUI.DisplayInfo("Chưa có combo nào!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("DANH SÁCH COMBO:");
            foreach (var cb in activeCombos.Take(10)) // 🔹 đổi combo -> cb để tránh nhầm
            {
                Console.WriteLine($"{cb.Id} - {cb.Name} - {cb.FinalPrice:N0}đ");
            }

            Console.Write("\nNhập mã combo: ");
            string comboId = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(comboId) ||
                !repository.Combos.ContainsKey(comboId) ||
                !repository.Combos[comboId].IsActive)
            {
                EnhancedUI.DisplayError("Combo không tồn tại!");
                Console.ReadKey();
                return;
            }

            var combo = repository.Combos[comboId];
            combo.CalculateOriginalPrice(repository.Dishes);
            combo.CalculateCost(repository.Dishes);

            Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                       CHI TIẾT COMBO                          ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Mã combo:", combo.Id);
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Tên combo:", combo.Name);
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Mô tả:", TruncateString(combo.Description, 30));
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Giảm giá:", $"{combo.DiscountPercent}%");
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Giá gốc:", $"{combo.OriginalPrice:N0}đ");
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Giá KM:", $"{combo.FinalPrice:N0}đ");
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Tiết kiệm:", $"{combo.OriginalPrice - combo.FinalPrice:N0}đ");
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Chi phí:", $"{combo.Cost:N0}đ");
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Lợi nhuận:", $"{combo.ProfitMargin:F1}%");
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Số lượt bán:", combo.SalesCount);
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

            Console.WriteLine("║ {0,-40} ║", "DANH SÁCH MÓN TRONG COMBO:");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

            if (combo.DishIds.Any())
            {
                foreach (var dishId in combo.DishIds)
                {
                    if (repository.Dishes.ContainsKey(dishId))
                    {
                        var dish = repository.Dishes[dishId];
                        string status = CheckDishIngredients(dish) ? "✅" : "⚠️";
                        Console.WriteLine("║ {0,-2} {1,-25} {2,-12} ║",
                            status,
                            TruncateString(dish.Name, 25),
                            $"{dish.Price:N0}đ");
                    }
                }
            }
            else
            {
                Console.WriteLine("║ {0,-40} ║", "Chưa có món trong combo");
            }

            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }


        private void GeneratePartyMenuCombos()
        {
            EnhancedUI.DisplayHeader("COMBO THỰC ĐƠN TIỆC");

            Console.WriteLine("Chọn loại tiệc:");
            Console.WriteLine("1. Tiệc cưới");
            Console.WriteLine("2. Tiệc sinh nhật");
            Console.WriteLine("3. Tiệc công ty");
            Console.WriteLine("4. Tiệc gia đình");
            Console.Write("Chọn: ");

            string choice = Console.ReadLine();
            List<Combo> suggestedCombos = new List<Combo>();
            string partyType = "";

            switch (choice)
            {
                case "1":
                    partyType = "Tiệc cưới";
                    suggestedCombos = GenerateWeddingCombos();
                    break;
                case "2":
                    partyType = "Tiệc sinh nhật";
                    suggestedCombos = GenerateBirthdayCombos();
                    break;
                case "3":
                    partyType = "Tiệc công ty";
                    suggestedCombos = GenerateCorporateCombos();
                    break;
                case "4":
                    partyType = "Tiệc gia đình";
                    suggestedCombos = GenerateFamilyCombos();
                    break;
                default:
                    EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                    return;
            }

            if (suggestedCombos.Any())
            {
                Console.WriteLine($"\n🎉 COMBO GỢI Ý CHO {partyType.ToUpper()}:");
                foreach (var combo in suggestedCombos)
                {
                    combo.CalculateOriginalPrice(repository.Dishes);
                    combo.CalculateCost(repository.Dishes);

                    Console.WriteLine($"\n{combo.Name}:");
                    Console.WriteLine($"- Giá gốc: {combo.OriginalPrice:N0}đ");
                    Console.WriteLine($"- Giá KM: {combo.FinalPrice:N0}đ");
                    Console.WriteLine($"- Giảm giá: {combo.DiscountPercent}%");
                    Console.WriteLine($"- Số món: {combo.DishIds.Count}");
                    Console.WriteLine($"- Lợi nhuận: {combo.ProfitMargin:F1}%");

                    Console.WriteLine("  Món bao gồm:");
                    foreach (var dishId in combo.DishIds)
                    {
                        if (repository.Dishes.ContainsKey(dishId))
                        {
                            var dish = repository.Dishes[dishId];
                            Console.WriteLine($"  + {dish.Name} - {dish.Price:N0}đ");
                        }
                    }
                }

                // Xuất file gợi ý
                if (EnhancedUI.Confirm("Xuất danh sách combo ra file?"))
                {
                    ExportPartyMenuCombos(suggestedCombos, partyType);
                }

                // Tạo combo trong hệ thống
                if (EnhancedUI.Confirm("Tạo các combo này trong hệ thống?"))
                {
                    foreach (var combo in suggestedCombos)
                    {
                        repository.Combos[combo.Id] = combo;
                    }
                    SaveAllData();
                    EnhancedUI.DisplaySuccess("Đã tạo combo trong hệ thống!");
                }
            }
            else
            {
                EnhancedUI.DisplayError("Không thể tạo combo cho loại tiệc này!");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private List<Combo> GenerateWeddingCombos()
        {
            var combos = new List<Combo>();

            // Combo cao cấp
            var premiumCombo = new Combo("WEDDING_PREMIUM", "Combo Cưới Cao Cấp", "Combo cao cấp cho tiệc cưới", 15);
            var premiumDishes = repository.Dishes.Values
                .Where(d => d.Price > 100000 && d.Category != "Đồ uống" && d.IsAvailable && CheckDishIngredients(d))
                .Take(4)
                .ToList();

            if (premiumDishes.Count >= 3)
            {
                foreach (var dish in premiumDishes)
                {
                    premiumCombo.DishIds.Add(dish.Id);
                }
                combos.Add(premiumCombo);
            }

            // Combo tiêu chuẩn
            var standardCombo = new Combo("WEDDING_STANDARD", "Combo Cưới Tiêu Chuẩn", "Combo tiêu chuẩn cho tiệc cưới", 10);
            var standardDishes = repository.Dishes.Values
                .Where(d => d.Price >= 50000 && d.Price <= 100000 && d.IsAvailable && CheckDishIngredients(d))
                .Take(3)
                .ToList();

            if (standardDishes.Count >= 2)
            {
                foreach (var dish in standardDishes)
                {
                    standardCombo.DishIds.Add(dish.Id);
                }
                combos.Add(standardCombo);
            }

            return combos;
        }

        private List<Combo> GenerateBirthdayCombos()
        {
            var combos = new List<Combo>();

            // Combo gia đình
            var familyCombo = new Combo("BIRTHDAY_FAMILY", "Combo Sinh Nhật Gia Đình", "Combo ấm cúng cho gia đình", 12);
            var familyDishes = repository.Dishes.Values
                .Where(d => (d.Category == "Món chính" || d.Category == "Món phụ") && d.IsAvailable && CheckDishIngredients(d))
                .Take(3)
                .ToList();

            if (familyDishes.Count >= 2)
            {
                foreach (var dish in familyDishes)
                {
                    familyCombo.DishIds.Add(dish.Id);
                }
                combos.Add(familyCombo);
            }

            return combos;
        }

        private List<Combo> GenerateCorporateCombos()
        {
            var combos = new List<Combo>();

            // Combo hội nghị
            var conferenceCombo = new Combo("CORP_CONFERENCE", "Combo Hội Nghị", "Combo chuyên nghiệp cho hội nghị", 8);
            var conferenceDishes = repository.Dishes.Values
                .Where(d => (d.Category == "Món khai vị" || d.Category == "Đồ uống") && d.IsAvailable && CheckDishIngredients(d))
                .Take(4)
                .ToList();

            if (conferenceDishes.Count >= 3)
            {
                foreach (var dish in conferenceDishes)
                {
                    conferenceCombo.DishIds.Add(dish.Id);
                }
                combos.Add(conferenceCombo);
            }

            return combos;
        }

        private List<Combo> GenerateFamilyCombos()
        {
            var combos = new List<Combo>();

            // Combo ấm cúng
            var cozyCombo = new Combo("FAMILY_COZY", "Combo Gia Đình Ấm Cúng", "Combo ấm cúng cho bữa cơm gia đình", 5);
            var cozyDishes = repository.Dishes.Values
                .Where(d => (d.Category == "Món chính" || d.Category == "Món phụ") && d.IsAvailable && CheckDishIngredients(d))
                .Take(3)
                .ToList();

            if (cozyDishes.Count >= 2)
            {
                foreach (var dish in cozyDishes)
                {
                    cozyCombo.DishIds.Add(dish.Id);
                }
                combos.Add(cozyCombo);
            }

            return combos;
        }

        private void ExportPartyMenuCombos(List<Combo> combos, string partyType)
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"ComboThucDon_{partyType.Replace(" ", "_")}_{DateTime.Now:yyyyMMddHHmmss}.txt";
                string filePath = Path.Combine(downloadPath, fileName);

                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine($"COMBO THỰC ĐƠN {partyType.ToUpper()}");
                    writer.WriteLine($"Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                    writer.WriteLine("==========================================");
                    writer.WriteLine();

                    foreach (var combo in combos)
                    {
                        combo.CalculateOriginalPrice(repository.Dishes);
                        writer.WriteLine($"{combo.Name}:");
                        writer.WriteLine($"  - Giá gốc: {combo.OriginalPrice:N0}đ");
                        writer.WriteLine($"  - Giá khuyến mãi: {combo.FinalPrice:N0}đ");
                        writer.WriteLine($"  - Giảm giá: {combo.DiscountPercent}%");
                        writer.WriteLine($"  - Số món: {combo.DishIds.Count}");
                        writer.WriteLine("  - Danh sách món:");

                        foreach (var dishId in combo.DishIds)
                        {
                            if (repository.Dishes.ContainsKey(dishId))
                            {
                                var dish = repository.Dishes[dishId];
                                writer.WriteLine($"    + {dish.Name} - {dish.Price:N0}đ");
                            }
                        }
                        writer.WriteLine();
                    }
                }

                EnhancedUI.DisplaySuccess($"Đã xuất file: {fileName}");
                Logger.Info($"Party menu combos exported: {fileName}", "ComboManagement");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to export party menu combos", "ComboManagement", ex);
                EnhancedUI.DisplayError($"Lỗi xuất file: {ex.Message}");
            }
        }

        // ==================== ORDER MANAGEMENT METHODS ====================
        private void ShowOrderManagementMenu()
        {
            var menuOptions = new List<string>
    {
        "Tạo đơn hàng mới",
        "Xem danh sách đơn hàng",
        "Cập nhật trạng thái đơn hàng",
        "Xem chi tiết đơn hàng",
        "Thống kê đơn hàng",
        "Xuất danh sách đơn hàng",
        "Tìm kiếm đơn hàng",
        "Hủy đơn hàng"
    };

            while (true)
            {
                int choice = EnhancedUI.ShowMenu("BÁN HÀNG / ĐƠN ĐẶT MÓN", menuOptions);
                if (choice == 0) return;

                switch (choice)
                {
                    case 1: CreateOrder(); break;
                    case 2: DisplayOrders(); break;
                    case 3: UpdateOrderStatus(); break;
                    case 4: ShowOrderDetail(); break;
                    case 5: ShowOrderStatistics(); break;
                    case 6: ExportOrders(); break;
                    case 7: SearchOrders(); break;
                    case 8: CancelOrder(); break;
                }
            }
        }

        private void CreateOrder()
        {
            EnhancedUI.DisplayHeader("TẠO ĐƠN HÀNG MỚI");

            try
            {
                string orderId = "ORD_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                Console.Write("Tên khách hàng: ");
                string customerName = Console.ReadLine();

                if (string.IsNullOrEmpty(customerName))
                {
                    EnhancedUI.DisplayError("Tên khách hàng không được để trống!");
                    return;
                }

                Console.Write("Số điện thoại: ");
                string customerPhone = Console.ReadLine();

                Console.Write("Địa chỉ: ");
                string customerAddress = Console.ReadLine();

                var order = new Order(orderId, customerName, currentUser.Username)
                {
                    CustomerPhone = customerPhone,
                    CustomerAddress = customerAddress
                };

                // Thêm món/combo vào đơn hàng
                while (true)
                {
                    Console.WriteLine("\n1. Thêm món ăn");
                    Console.WriteLine("2. Thêm combo");
                    Console.WriteLine("3. Xem đơn hàng hiện tại");
                    Console.WriteLine("4. Áp dụng khuyến mãi");
                    Console.WriteLine("5. Kết thúc");
                    Console.Write("Chọn: ");

                    string choice = Console.ReadLine();
                    if (choice == "5") break;

                    switch (choice)
                    {
                        case "1":
                            AddDishToOrder(order);
                            break;
                        case "2":
                            AddComboToOrder(order);
                            break;
                        case "3":
                            ShowCurrentOrder(order);
                            break;
                        case "4":
                            ApplyDiscount(order);
                            break;
                        default:
                            EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                            break;
                    }
                }

                if (order.Items.Count == 0)
                {
                    EnhancedUI.DisplayError("Đơn hàng phải có ít nhất 1 món!");
                    return;
                }

                // Hiển thị tổng quan đơn hàng
                ShowCurrentOrder(order);

                if (EnhancedUI.Confirm("Xác nhận tạo đơn hàng?"))
                {
                    var command = new CreateOrderCommand(this, order);
                    undoRedoService.ExecuteCommand(command);

                    repository.AuditLogs.Add(new AuditLog(currentUser.Username, "CREATE_ORDER", "ORDER", orderId,
                        $"Tạo đơn: {customerName} - {order.FinalAmount:N0}đ"));
                    SaveAllData();

                    EnhancedUI.DisplaySuccess($"🎉 TẠO ĐƠN HÀNG THÀNH CÔNG!");
                    Console.WriteLine($"📋 Mã đơn: {orderId}");
                    Console.WriteLine($"👤 Khách hàng: {customerName}");
                    Console.WriteLine($"💰 Tổng tiền: {order.TotalAmount:N0}đ");
                    if (order.DiscountAmount > 0)
                    {
                        Console.WriteLine($"🎁 Giảm giá: {order.DiscountAmount:N0}đ");
                    }
                    Console.WriteLine($"💳 Thành tiền: {order.FinalAmount:N0}đ");
                    Console.WriteLine($"👨‍💼 Nhân viên: {currentUser.FullName}");

                    // Xuất hóa đơn
                    if (EnhancedUI.Confirm("Xuất hóa đơn?"))
                    {
                        ExportOrderInvoice(order);
                    }

                    Logger.Info($"Order {orderId} created successfully", "OrderManagement");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create order", "OrderManagement", ex);
                EnhancedUI.DisplayError($"Lỗi: {ex.Message}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void AddDishToOrder(Order order)
        {
            int currentPage = 1;
            const int pageSize = 10;
            var dishes = repository.Dishes.Values.ToList();
            int totalPages = (int)Math.Ceiling((double)dishes.Count / pageSize);

            while (true)
            {
                if (currentPage < 1) currentPage = 1;
                else if (currentPage > totalPages) currentPage = totalPages;

                var pageDishes = dishes.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();

                EnhancedUI.DisplayHeader($"🍽️ DANH SÁCH MÓN ĂN - TRANG {currentPage}/{totalPages}");
                Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║ MÃ MÓN │ TÊN MÓN                           │ GIÁ (VNĐ) │ TRẠNG THÁI ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

                foreach (var dish in pageDishes)
                {
                    string status = dish.IsAvailable ? "✅" : "❌";
                    Console.WriteLine($"║ {dish.Id,-6} │ {dish.Name,-30} │ {dish.Price,10:N0} │ {status,8} ║");
                }

                Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
                Console.WriteLine("\nNhập mã món để thêm | Nhập số trang để chuyển | Enter để quay lại");
                Console.Write("👉 Lựa chọn: ");
                string input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input)) break;

                // Nếu nhập số trang
                if (int.TryParse(input, out int pageNum))
                {
                    if (pageNum >= 1 && pageNum <= totalPages)
                    {
                        currentPage = pageNum;
                        continue;
                    }
                    else
                    {
                        EnhancedUI.DisplayError("❌ Số trang không hợp lệ!");
                        Console.ReadKey();
                        continue;
                    }
                }

                string dishId = input;
                if (!repository.Dishes.ContainsKey(dishId))
                {
                    EnhancedUI.DisplayError("❌ Món ăn không tồn tại!");
                    Console.ReadKey();
                    continue;
                }

                var dishToAdd = repository.Dishes[dishId];
                if (!dishToAdd.IsAvailable)
                {
                    EnhancedUI.DisplayError("❌ Món ăn hiện không khả dụng!");
                    Console.ReadKey();
                    continue;
                }

                if (!CheckDishIngredients(dishToAdd))
                {
                    EnhancedUI.DisplayError("❌ Nguyên liệu không đủ để làm món này!");
                    Console.ReadKey();
                    continue;
                }

                Console.Write("Nhập số lượng: ");
                if (!int.TryParse(Console.ReadLine(), out int quantity) || quantity <= 0)
                {
                    EnhancedUI.DisplayError("⚠️ Số lượng không hợp lệ!");
                    Console.ReadKey();
                    continue;
                }

                var orderItem = new OrderItem
                {
                    ItemId = dishId,
                    IsCombo = false,
                    Quantity = quantity,
                    UnitPrice = dishToAdd.Price,
                    ItemName = dishToAdd.Name
                };

                order.Items.Add(orderItem);
                EnhancedUI.DisplaySuccess($"✅ Đã thêm {quantity} x {dishToAdd.Name} vào đơn hàng!");
                if (!EnhancedUI.Confirm("Thêm món khác?")) break;
            }
        }



        private void AddComboToOrder(Order order)
        {
            var activeCombos = repository.Combos.Values.Where(c => c.IsActive).ToList();
            if (!activeCombos.Any())
            {
                EnhancedUI.DisplayError("Không có combo nào khả dụng!");
                return;
            }

            Console.WriteLine("DANH SÁCH COMBO:");
            foreach (var cb in activeCombos.Take(10)) // 🔹 đổi combo → cb
            {
                cb.CalculateOriginalPrice(repository.Dishes);
                Console.WriteLine($"{cb.Id} - {cb.Name} - {cb.FinalPrice:N0}đ - {cb.DishIds.Count} món");
            }

            Console.Write("\nNhập mã combo: ");
            string comboId = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(comboId) ||
                !repository.Combos.ContainsKey(comboId) ||
                !repository.Combos[comboId].IsActive)
            {
                EnhancedUI.DisplayError("Combo không tồn tại hoặc không khả dụng!");
                return;
            }

            var combo = repository.Combos[comboId];

            // 🔹 Kiểm tra nguyên liệu
            foreach (var dishId in combo.DishIds)
            {
                if (!repository.Dishes.ContainsKey(dishId))
                {
                    EnhancedUI.DisplayError($"Combo chứa món không tồn tại (ID: {dishId})!");
                    return;
                }

                var dish = repository.Dishes[dishId];
                if (!CheckDishIngredients(dish))
                {
                    EnhancedUI.DisplayError($"Không đủ nguyên liệu cho món '{dish.Name}' trong combo này!");
                    return;
                }
            }

            Console.Write("Số lượng: ");
            if (!int.TryParse(Console.ReadLine(), out int quantity) || quantity <= 0)
            {
                EnhancedUI.DisplayError("Số lượng không hợp lệ!");
                return;
            }

            combo.CalculateOriginalPrice(repository.Dishes);

            var orderItem = new OrderItem
            {
                ItemId = comboId,
                IsCombo = true,
                Quantity = quantity,
                UnitPrice = combo.FinalPrice,
                ItemName = combo.Name
            };

            order.Items.Add(orderItem);

            EnhancedUI.DisplaySuccess($"Đã thêm {quantity} combo '{combo.Name}' vào đơn hàng thành công!");
        }


        private void ShowCurrentOrder(Order order)
        {
            Console.WriteLine("\n📋 ĐƠN HÀNG HIỆN TẠI:");
            Console.WriteLine("═══════════════════════════════════════════════════════");

            foreach (var item in order.Items)
            {
                string itemName = item.IsCombo ? $"[COMBO] {item.ItemName}" : item.ItemName;
                Console.WriteLine($"- {itemName} x{item.Quantity} = {item.TotalPrice:N0}đ");
            }

            Console.WriteLine("───────────────────────────────────────────────────────");
            Console.WriteLine($"💰 TỔNG TIỀN: {order.TotalAmount:N0}đ");

            if (order.DiscountAmount > 0)
            {
                Console.WriteLine($"🎁 GIẢM GIÁ: {order.DiscountAmount:N0}đ");
                Console.WriteLine($"💳 THÀNH TIỀN: {order.FinalAmount:N0}đ");
            }

            Console.WriteLine("═══════════════════════════════════════════════════════");
        }

        private void ApplyDiscount(Order order)
        {
            if (order.Items.Count == 0)
            {
                EnhancedUI.DisplayError("Đơn hàng chưa có món nào!");
                return;
            }

            Console.Write("Số tiền giảm giá: ");
            if (decimal.TryParse(Console.ReadLine(), out decimal discount) && discount >= 0)
            {
                if (discount > order.TotalAmount)
                {
                    EnhancedUI.DisplayError("Số tiền giảm giá không thể lớn hơn tổng tiền!");
                    return;
                }

                order.DiscountAmount = discount;
                EnhancedUI.DisplaySuccess($"Đã áp dụng giảm giá {discount:N0}đ!");
                ShowCurrentOrder(order);
            }
            else
            {
                EnhancedUI.DisplayError("Số tiền không hợp lệ!");
            }
        }

        private void DisplayOrders(int page = 1, int pageSize = 20)
        {
            EnhancedUI.DisplayHeader("DANH SÁCH ĐƠN HÀNG");

            var orderList = repository.Orders.Values.ToList();
            int totalPages = (int)Math.Ceiling(orderList.Count / (double)pageSize);

            if (orderList.Count == 0)
            {
                EnhancedUI.DisplayInfo("Chưa có đơn hàng nào!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                               DANH SÁCH ĐƠN HÀNG                             ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-15} {1,-15} {2,-8} {3,-12} {4,-15} ║",
                "Mã đơn", "Khách hàng", "Số món", "Tổng tiền", "Trạng thái");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

            var pagedOrders = orderList.OrderByDescending(o => o.OrderDate)
                                       .Skip((page - 1) * pageSize)
                                       .Take(pageSize);

            foreach (var order in pagedOrders)
            {
                string status = GetOrderStatusText(order.Status);
                Console.WriteLine("║ {0,-15} {1,-15} {2,-8} {3,-12} {4,-15} ║",
                    order.Id,
                    TruncateString(order.CustomerName, 15),
                    order.Items.Count,
                    $"{order.FinalAmount:N0}đ",
                    status);
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine($"\nTrang {page}/{totalPages} - Tổng cộng: {orderList.Count} đơn hàng");

            if (page > 1) Console.Write("[P] Trang trước | ");
            if (page < totalPages) Console.Write("[N] Trang sau | ");
            Console.WriteLine("[0] Thoát");
            Console.Write("Chọn: ");

            string choice = Console.ReadLine().ToLower();
            if (choice == "n" && page < totalPages)
                DisplayOrders(page + 1, pageSize);
            else if (choice == "p" && page > 1)
                DisplayOrders(page - 1, pageSize);
        }

        private string GetOrderStatusText(OrderStatus status)
        {
            switch (status)
            {
                case OrderStatus.Pending: return "⏳ Chờ xử lý";
                case OrderStatus.Processing: return "👨‍🍳 Đang chế biến";
                case OrderStatus.Completed: return "✅ Hoàn thành";
                case OrderStatus.Cancelled: return "❌ Đã hủy";
                default: return "Unknown";
            }
        }

        private void UpdateOrderStatus()
        {
            EnhancedUI.DisplayHeader("CẬP NHẬT TRẠNG THÁI ĐƠN HÀNG");

            var recentOrders = repository.Orders.Values
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .ToList();

            if (!recentOrders.Any())
            {
                EnhancedUI.DisplayInfo("Chưa có đơn hàng nào!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("ĐƠN HÀNG GẦN ĐÂY:");
            foreach (var o in recentOrders) // ✅ Đổi 'order' thành 'o'
            {
                string status = GetOrderStatusText(o.Status);
                Console.WriteLine($"{o.Id} - {o.CustomerName} - {o.FinalAmount:N0}đ - {status}");
            }

            Console.Write("\nNhập mã đơn hàng: ");
            string orderId = Console.ReadLine();

            if (!repository.Orders.ContainsKey(orderId))
            {
                EnhancedUI.DisplayError("Đơn hàng không tồn tại!");
                Console.ReadKey();
                return;
            }

            var order = repository.Orders[orderId]; // Không còn trùng nữa ✅

            Console.WriteLine($"\nThông tin đơn hàng:");
            Console.WriteLine($"- Mã đơn: {order.Id}");
            Console.WriteLine($"- Khách hàng: {order.CustomerName}");
            Console.WriteLine($"- Tổng tiền: {order.FinalAmount:N0}đ");
            Console.WriteLine($"- Trạng thái hiện tại: {GetOrderStatusText(order.Status)}");

            Console.WriteLine("\nChọn trạng thái mới:");
            Console.WriteLine("1. ⏳ Chờ xử lý");
            Console.WriteLine("2. 👨‍🍳 Đang chế biến");
            Console.WriteLine("3. ✅ Hoàn thành");
            Console.WriteLine("4. ❌ Hủy đơn");
            Console.Write("Chọn: ");

            string choice = Console.ReadLine();
            OrderStatus newStatus = order.Status;

            switch (choice)
            {
                case "1": newStatus = OrderStatus.Pending; break;
                case "2": newStatus = OrderStatus.Processing; break;
                case "3":
                    newStatus = OrderStatus.Completed;
                    order.CompletedDate = DateTime.Now;
                    break;
                case "4": newStatus = OrderStatus.Cancelled; break;
                default:
                    EnhancedUI.DisplayError("Lựa chọn không hợp lệ!");
                    return;
            }

            var command = new UpdateOrderStatusCommand(this, order, newStatus);
            undoRedoService.ExecuteCommand(command);

            repository.AuditLogs.Add(new AuditLog(currentUser.Username, "UPDATE_ORDER_STATUS", "ORDER", orderId,
                $"Cập nhật trạng thái: {GetOrderStatusText(newStatus)}"));
            SaveAllData();

            EnhancedUI.DisplaySuccess($"Cập nhật trạng thái thành công: {GetOrderStatusText(newStatus)}");
            Logger.Info($"Order {orderId} status updated to {newStatus}", "OrderManagement");

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }


        private void ShowOrderDetail()
        {
            EnhancedUI.DisplayHeader("CHI TIẾT ĐƠN HÀNG");

            var recentOrders = repository.Orders.Values
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .ToList();

            if (!recentOrders.Any())
            {
                EnhancedUI.DisplayInfo("Chưa có đơn hàng nào!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("ĐƠN HÀNG GẦN ĐÂY:");
            foreach (var o in recentOrders) // ✅ đổi tên biến ở đây
            {
                Console.WriteLine($"{o.Id} - {o.CustomerName} - {o.FinalAmount:N0}đ");
            }

            Console.Write("\nNhập mã đơn hàng: ");
            string orderId = Console.ReadLine();

            if (!repository.Orders.ContainsKey(orderId))
            {
                EnhancedUI.DisplayError("Đơn hàng không tồn tại!");
                Console.ReadKey();
                return;
            }

            var order = repository.Orders[orderId]; // ✅ không còn bị trùng tên

            Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                       CHI TIẾT ĐƠN HÀNG                        ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-15} {1,-30}                 ║", "Mã đơn:", order.Id);
            Console.WriteLine("║ {0,-15} {1,-30}                 ║", "Khách hàng:", order.CustomerName);
            Console.WriteLine("║ {0,-15} {1,-30}                 ║", "Điện thoại:", order.CustomerPhone);
            Console.WriteLine("║ {0,-15} {1,-30}                 ║", "Địa chỉ:", order.CustomerAddress);
            Console.WriteLine("║ {0,-15} {1,-30}                 ║", "Nhân viên:", order.StaffUsername);
            Console.WriteLine("║ {0,-15} {1,-30}                 ║", "Ngày đặt:", order.OrderDate.ToString("dd/MM/yyyy HH:mm"));
            Console.WriteLine("║ {0,-15} {1,-30}                ║", "Trạng thái:", GetOrderStatusText(order.Status));

            if (order.CompletedDate.HasValue)
            {
                Console.WriteLine("║ {0,-15} {1,-30} ║", "Hoàn thành:", order.CompletedDate.Value.ToString("dd/MM/yyyy HH:mm"));
            }

            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-30} {1,-10} {2,-15} {3,-10} ║",
                "Tên món/combo", "Số lượng", "Đơn giá", "Thành tiền");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

            foreach (var item in order.Items)
            {
                Console.WriteLine("║ {0,-30} {1,-10} {2,-15} {3,-10} ║",
                    TruncateString(item.ItemName, 30),
                    item.Quantity,
                    $"{item.UnitPrice:N0}đ",
                    $"{item.TotalPrice:N0}đ");
            }

            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-30} {1,-10} {2,-15} {3,-10} ║",
                "TỔNG CỘNG", "", "", $"{order.TotalAmount:N0}đ");

            if (order.DiscountAmount > 0)
            {
                Console.WriteLine("║ {0,-30} {1,-10} {2,-15} {3,-10} ║",
                    "GIẢM GIÁ", "", "", $"-{order.DiscountAmount:N0}đ");
                Console.WriteLine("║ {0,-30} {1,-10} {2,-15} {3,-10} ║",
                    "THÀNH TIỀN", "", "", $"{order.FinalAmount:N0}đ");
            }

            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }


        private void ShowOrderStatistics()
        {
            EnhancedUI.DisplayHeader("THỐNG KÊ ĐƠN HÀNG");

            var completedOrders = repository.Orders.Values.Where(o => o.Status == OrderStatus.Completed).ToList();
            var today = DateTime.Today;

            var dailyOrders = completedOrders.Where(o => o.CompletedDate?.Date == today).ToList();
            var weeklyOrders = completedOrders.Where(o => o.CompletedDate?.Date >= today.AddDays(-7)).ToList();
            var monthlyOrders = completedOrders.Where(o => o.CompletedDate?.Date >= today.AddDays(-30)).ToList();

            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                      THỐNG KÊ DOANH THU                       ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-25} {1,-15} {2,-15} ║", "Thời gian", "Số đơn", "Doanh thu");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-25} {1,-15} {2,-15} ║", "Hôm nay", dailyOrders.Count, $"{dailyOrders.Sum(o => o.FinalAmount):N0}đ");
            Console.WriteLine("║ {0,-25} {1,-15} {2,-15} ║", "7 ngày qua", weeklyOrders.Count, $"{weeklyOrders.Sum(o => o.FinalAmount):N0}đ");
            Console.WriteLine("║ {0,-25} {1,-15} {2,-15} ║", "30 ngày qua", monthlyOrders.Count, $"{monthlyOrders.Sum(o => o.FinalAmount):N0}đ");
            Console.WriteLine("║ {0,-25} {1,-15} {2,-15} ║", "Tổng cộng", completedOrders.Count, $"{completedOrders.Sum(o => o.FinalAmount):N0}đ");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

            // Phân bổ trạng thái
            var statusGroups = repository.Orders.Values.GroupBy(o => o.Status);
            Console.WriteLine("\n📊 PHÂN BỔ TRẠNG THÁI ĐƠN HÀNG:");
            foreach (var group in statusGroups)
            {
                Console.WriteLine($"{GetOrderStatusText(group.Key)}: {group.Count()} đơn");
            }

            // Top món bán chạy
            var topDishes = repository.Dishes.Values.OrderByDescending(d => d.SalesCount).Take(5);
            Console.WriteLine("\n🏆 TOP 5 MÓN BÁN CHẠY:");
            foreach (var dish in topDishes)
            {
                Console.WriteLine($"- {dish.Name}: {dish.SalesCount} lượt - {dish.Price * dish.SalesCount:N0}đ");
            }

            // Top combo bán chạy
            var topCombos = repository.Combos.Values.Where(c => c.IsActive).OrderByDescending(c => c.SalesCount).Take(3);
            Console.WriteLine("\n🎁 TOP 3 COMBO BÁN CHẠY:");
            foreach (var combo in topCombos)
            {
                combo.CalculateOriginalPrice(repository.Dishes);
                Console.WriteLine($"- {combo.Name}: {combo.SalesCount} lượt - {combo.FinalPrice * combo.SalesCount:N0}đ");
            }

            Logger.Info("Order statistics generated", "OrderManagement");
            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ExportOrders()
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"DanhSachDonHang_{DateTime.Now:yyyyMMddHHmmss}.csv";
                string filePath = Path.Combine(downloadPath, fileName);

                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("Mã đơn,Khách hàng,Điện thoại,Nhân viên,Ngày đặt,Trạng thái,Tổng tiền,Giảm giá,Thành tiền,Số món");

                    foreach (var order in repository.Orders.Values.OrderByDescending(o => o.OrderDate))
                    {
                        writer.WriteLine($"{order.Id},{order.CustomerName},{order.CustomerPhone},{order.StaffUsername},{order.OrderDate:dd/MM/yyyy HH:mm},{order.Status},{order.TotalAmount},{order.DiscountAmount},{order.FinalAmount},{order.Items.Count}");
                    }
                }

                EnhancedUI.DisplaySuccess($"Đã xuất danh sách đơn hàng: {fileName}");
                repository.AuditLogs.Add(new AuditLog(currentUser.Username, "EXPORT_ORDERS", "SYSTEM", "", "Xuất danh sách đơn hàng"));
                Logger.Info($"Orders exported to {fileName}", "OrderManagement");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to export orders", "OrderManagement", ex);
                EnhancedUI.DisplayError($"Lỗi khi xuất file: {ex.Message}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void SearchOrders()
        {
            EnhancedUI.DisplayHeader("TÌM KIẾM ĐƠN HÀNG");

            Console.Write("Nhập từ khóa tìm kiếm (mã đơn, tên khách, số điện thoại): ");
            string keyword = Console.ReadLine().ToLower();

            if (string.IsNullOrEmpty(keyword))
            {
                EnhancedUI.DisplayError("Vui lòng nhập từ khóa tìm kiếm!");
                Console.ReadKey();
                return;
            }

            var results = repository.Orders.Values.Where(o =>
                o.Id.ToLower().Contains(keyword) ||
                o.CustomerName.ToLower().Contains(keyword) ||
                o.CustomerPhone.ToLower().Contains(keyword) ||
                o.StaffUsername.ToLower().Contains(keyword)).ToList();

            Console.WriteLine($"\nTìm thấy {results.Count} kết quả cho '{keyword}':");

            if (results.Any())
            {
                Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                     KẾT QUẢ TÌM KIẾM                        ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
                Console.WriteLine("║ {0,-15} {1,-15} {2,-12} {3,-15} ║",
                    "Mã đơn", "Khách hàng", "Tổng tiền", "Trạng thái");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

                foreach (var order in results.Take(10))
                {
                    string status = GetOrderStatusText(order.Status);
                    Console.WriteLine("║ {0,-15} {1,-15} {2,-12} {3,-15} ║",
                        order.Id,
                        TruncateString(order.CustomerName, 15),
                        $"{order.FinalAmount:N0}đ",
                        status);
                }
                Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            }

            Logger.Info($"Searched orders with keyword: {keyword} - Found {results.Count} results", "OrderManagement");
            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void CancelOrder()
        {
            EnhancedUI.DisplayHeader("HỦY ĐƠN HÀNG");

            var pendingOrders = repository.Orders.Values
                .Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Processing)
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .ToList();

            if (!pendingOrders.Any())
            {
                EnhancedUI.DisplayInfo("Không có đơn hàng nào để hủy!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("ĐƠN HÀNG CÓ THỂ HỦY:");
            foreach (var o in pendingOrders) // ✅ đổi tên biến để tránh trùng
            {
                string status = GetOrderStatusText(o.Status);
                Console.WriteLine($"{o.Id} - {o.CustomerName} - {o.FinalAmount:N0}đ - {status}");
            }

            Console.Write("\nNhập mã đơn hàng cần hủy: ");
            string orderId = Console.ReadLine();

            if (!repository.Orders.ContainsKey(orderId))
            {
                EnhancedUI.DisplayError("Đơn hàng không tồn tại!");
                Console.ReadKey();
                return;
            }

            var order = repository.Orders[orderId]; // ✅ không còn bị trùng tên

            if (order.Status == OrderStatus.Completed)
            {
                EnhancedUI.DisplayError("Không thể hủy đơn hàng đã hoàn thành!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"\nThông tin đơn hàng:");
            Console.WriteLine($"- Khách hàng: {order.CustomerName}");
            Console.WriteLine($"- Tổng tiền: {order.FinalAmount:N0}đ");
            Console.WriteLine($"- Số món: {order.Items.Count}");

            if (EnhancedUI.Confirm("Xác nhận hủy đơn hàng này?"))
            {
                var command = new UpdateOrderStatusCommand(this, order, OrderStatus.Cancelled);
                undoRedoService.ExecuteCommand(command);

                // Hoàn trả nguyên liệu
                foreach (var item in order.Items)
                {
                    if (!item.IsCombo)
                    {
                        if (repository.Dishes.ContainsKey(item.ItemId))
                        {
                            var dish = repository.Dishes[item.ItemId];
                            foreach (var ing in dish.Ingredients)
                            {
                                if (repository.Ingredients.ContainsKey(ing.Key))
                                {
                                    repository.Ingredients[ing.Key].Quantity += ing.Value * item.Quantity;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (repository.Combos.ContainsKey(item.ItemId))
                        {
                            var combo = repository.Combos[item.ItemId];
                            foreach (var dishId in combo.DishIds)
                            {
                                if (repository.Dishes.ContainsKey(dishId))
                                {
                                    var dish = repository.Dishes[dishId];
                                    foreach (var ing in dish.Ingredients)
                                    {
                                        if (repository.Ingredients.ContainsKey(ing.Key))
                                        {
                                            repository.Ingredients[ing.Key].Quantity += ing.Value * item.Quantity;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                repository.AuditLogs.Add(new AuditLog(currentUser.Username, "CANCEL_ORDER", "ORDER", orderId, "Hủy đơn hàng"));
                SaveAllData();

                EnhancedUI.DisplaySuccess("Đã hủy đơn hàng thành công!");
                Logger.Info($"Order {orderId} cancelled", "OrderManagement");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }


        private void ExportOrderInvoice(Order order)
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"HoaDon_{order.Id}.txt";
                string filePath = Path.Combine(downloadPath, fileName);

                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("╔════════════════════════════════════════════════════════════════╗");
                    writer.WriteLine("║                         HÓA ĐƠN                               ║");
                    writer.WriteLine("╠════════════════════════════════════════════════════════════════╣");
                    writer.WriteLine($"║ Mã đơn: {order.Id,-50} ║");
                    writer.WriteLine($"║ Khách hàng: {order.CustomerName,-43} ║");
                    writer.WriteLine($"║ Điện thoại: {order.CustomerPhone,-42} ║");
                    writer.WriteLine($"║ Địa chỉ: {order.CustomerAddress,-44} ║");
                    writer.WriteLine($"║ Nhân viên: {order.StaffUsername,-44} ║");
                    writer.WriteLine($"║ Ngày: {order.OrderDate:dd/MM/yyyy HH:mm,-41} ║");
                    writer.WriteLine("╠════════════════════════════════════════════════════════════════╣");
                    writer.WriteLine("║ Tên món/combo                  Số lượng   Đơn giá   Thành tiền║");
                    writer.WriteLine("╠════════════════════════════════════════════════════════════════╣");

                    foreach (var item in order.Items)
                    {
                        string itemName = item.IsCombo ? $"[COMBO] {item.ItemName}" : item.ItemName;
                        writer.WriteLine($"║ {TruncateString(itemName, 30),-30} {item.Quantity,-10} {item.UnitPrice,-9} {item.TotalPrice,-10} ║");
                    }

                    writer.WriteLine("╠════════════════════════════════════════════════════════════════╣");
                    writer.WriteLine($"║ TỔNG CỘNG: {order.TotalAmount,45}đ ║");

                    if (order.DiscountAmount > 0)
                    {
                        writer.WriteLine($"║ GIẢM GIÁ: {order.DiscountAmount,46}đ ║");
                        writer.WriteLine($"║ THÀNH TIỀN: {order.FinalAmount,43}đ ║");
                    }

                    writer.WriteLine("╚════════════════════════════════════════════════════════════════╝");
                    writer.WriteLine();
                    writer.WriteLine("           Cảm ơn quý khách và hẹn gặp lại!");
                    writer.WriteLine($"           {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                }

                EnhancedUI.DisplaySuccess($"📄 Đã xuất hóa đơn: {fileName}");
                Logger.Info($"Invoice exported: {fileName}", "OrderManagement");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to export invoice", "OrderManagement", ex);
                EnhancedUI.DisplayError($"Lỗi khi xuất hóa đơn: {ex.Message}");
            }
        }

        // ==================== REPORT MANAGEMENT METHODS ====================
        private void ShowReportMenu()
        {
            var menuOptions = new List<string>
    {
        "Thống kê món ăn theo nhóm",
        "Thống kê nguyên liệu",
        "Thống kê doanh thu",
        "Thống kê combo bán chạy",
        "Báo cáo tồn kho",
        "Báo cáo hiệu quả kinh doanh",
        "Xuất báo cáo tổng hợp",
        "Xuất lịch sử thao tác"
    };

            while (true)
            {
                int choice = EnhancedUI.ShowMenu("THỐNG KÊ & BÁO CÁO", menuOptions);
                if (choice == 0) return;

                switch (choice)
                {
                    case 1: ShowDishCategoryReport(); break;
                    case 2: ShowIngredientReport(); break;
                    case 3: ShowRevenueReport(); break;
                    case 4: ShowComboSalesReport(); break;
                    case 5: ShowInventoryReport(); break;
                    case 6: ShowBusinessEfficiencyReport(); break;
                    case 7: ExportComprehensiveReport(); break;
                    case 8: ExportAuditLogs(); break;
                }
            }
        }

        private void ShowDishCategoryReport()
        {
            EnhancedUI.DisplayHeader("THỐNG KÊ MÓN ĂN THEO NHÓM");

            var categoryGroups = repository.Dishes.Values
                .GroupBy(d => d.Category)
                .Select(g => new {
                    Category = g.Key,
                    Count = g.Count(),
                    TotalSales = g.Sum(d => d.SalesCount),
                    TotalRevenue = g.Sum(d => d.Price * d.SalesCount),
                    AvgPrice = g.Average(d => d.Price)
                })
                .OrderByDescending(g => g.Count)
                .ToList();

            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                THỐNG KÊ MÓN ĂN THEO NHÓM                                     ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-25} {1,-10} {2,-15} {3,-15} {4,-10} ║",
                "Nhóm món", "Số món", "Tổng lượt bán", "Doanh thu", "Giá TB");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

            foreach (var group in categoryGroups)
            {
                Console.WriteLine("║ {0,-25} {1,-10} {2,-15} {3,-15} {4,-10} ║",
                    TruncateString(group.Category, 25),
                    group.Count,
                    group.TotalSales,
                    $"{group.TotalRevenue:N0}đ",
                    $"{group.AvgPrice:N0}đ");
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");

            // Xuất file báo cáo
            if (EnhancedUI.Confirm("Xuất báo cáo ra file?"))
            {
                ExportDishCategoryReport(categoryGroups);
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ExportDishCategoryReport(dynamic categoryGroups)
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"BaoCaoNhomMon_{DateTime.Now:yyyyMMddHHmmss}.txt";
                string filePath = Path.Combine(downloadPath, fileName);

                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("BÁO CÁO PHÂN BỔ MÓN ĂN THEO NHÓM");
                    writer.WriteLine($"Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                    writer.WriteLine("==========================================");
                    writer.WriteLine();

                    foreach (var group in categoryGroups)
                    {
                        writer.WriteLine($"{group.Category}:");
                        writer.WriteLine($"  - Số món: {group.Count}");
                        writer.WriteLine($"  - Tổng lượt bán: {group.TotalSales}");
                        writer.WriteLine($"  - Doanh thu: {group.TotalRevenue:N0}đ");
                        writer.WriteLine($"  - Giá trung bình: {group.AvgPrice:N0}đ");
                        writer.WriteLine();
                    }
                }

                EnhancedUI.DisplaySuccess($"Đã xuất báo cáo: {fileName}");
                Logger.Info($"Dish category report exported: {fileName}", "Reports");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to export dish category report", "Reports", ex);
                EnhancedUI.DisplayError($"Lỗi xuất báo cáo: {ex.Message}");
            }
        }

        private void ShowIngredientReport()
        {
            EnhancedUI.DisplayHeader("BÁO CÁO NGUYÊN LIỆU");

            var lowStock = repository.Ingredients.Values.Where(ing => ing.IsLowStock).ToList();
            var outOfStock = repository.Ingredients.Values.Where(ing => ing.Quantity == 0).ToList();
            var sufficientStock = repository.Ingredients.Values.Where(ing => !ing.IsLowStock && ing.Quantity > 0).ToList();

            decimal totalValue = repository.Ingredients.Values.Sum(ing => ing.Quantity * ing.PricePerUnit);
            decimal avgPrice = repository.Ingredients.Values.Average(ing => ing.PricePerUnit);

            Console.WriteLine("📊 TỔNG QUAN NGUYÊN LIỆU:");
            Console.WriteLine($"- Tổng số nguyên liệu: {repository.Ingredients.Count}");
            Console.WriteLine($"- Đủ stock: {sufficientStock.Count}");
            Console.WriteLine($"- Sắp hết: {lowStock.Count}");
            Console.WriteLine($"- Hết hàng: {outOfStock.Count}");
            Console.WriteLine($"- Tổng giá trị tồn kho: {totalValue:N0}đ");
            Console.WriteLine($"- Giá trung bình: {avgPrice:N0}đ/đơn vị");

            if (lowStock.Any())
            {
                Console.WriteLine($"\n⚠️  NGUYÊN LIỆU SẮP HẾT ({lowStock.Count}):");
                foreach (var ing in lowStock.Take(5))
                {
                    decimal needed = ing.MinQuantity - ing.Quantity;
                    Console.WriteLine($"- {ing.Name}: {ing.Quantity} {ing.Unit} (Cần: {needed} {ing.Unit})");
                }
            }

            // Nguyên liệu có giá trị cao nhất
            var valuableIngredients = repository.Ingredients.Values
                .OrderByDescending(ing => ing.Quantity * ing.PricePerUnit)
                .Take(5)
                .ToList();

            Console.WriteLine($"\n💰 NGUYÊN LIỆU CÓ GIÁ TRỊ CAO NHẤT:");
            foreach (var ing in valuableIngredients)
            {
                decimal value = ing.Quantity * ing.PricePerUnit;
                Console.WriteLine($"- {ing.Name}: {value:N0}đ ({ing.Quantity} {ing.Unit})");
            }

            Logger.Info("Ingredient report generated", "Reports");
            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ShowRevenueReport()
        {
            EnhancedUI.DisplayHeader("BÁO CÁO DOANH THU");

            var completedOrders = repository.Orders.Values.Where(o => o.Status == OrderStatus.Completed).ToList();
            var today = DateTime.Today;

            // Doanh thu theo ngày
            var dailyRevenue = completedOrders
                .Where(o => o.CompletedDate?.Date == today)
                .Sum(o => o.FinalAmount);

            // Doanh thu theo tuần
            var weeklyRevenue = completedOrders
                .Where(o => o.CompletedDate?.Date >= today.AddDays(-7))
                .Sum(o => o.FinalAmount);

            // Doanh thu theo tháng
            var monthlyRevenue = completedOrders
                .Where(o => o.CompletedDate?.Date >= today.AddDays(-30))
                .Sum(o => o.FinalAmount);

            var totalRevenue = completedOrders.Sum(o => o.FinalAmount);

            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                      THỐNG KÊ DOANH THU                       ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-25} {1,-30} ║", "Hôm nay:", $"{dailyRevenue:N0}đ");
            Console.WriteLine("║ {0,-25} {1,-30} ║", "7 ngày qua:", $"{weeklyRevenue:N0}đ");
            Console.WriteLine("║ {0,-25} {1,-30} ║", "30 ngày qua:", $"{monthlyRevenue:N0}đ");
            Console.WriteLine("║ {0,-25} {1,-30} ║", "Tổng doanh thu:", $"{totalRevenue:N0}đ");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

            // Phân tích doanh thu theo thời gian
            var revenueByDay = completedOrders
                .Where(o => o.CompletedDate?.Date >= today.AddDays(-7))
                .GroupBy(o => o.CompletedDate?.Date)
                .Select(g => new { Date = g.Key, Revenue = g.Sum(o => o.FinalAmount) })
                .OrderBy(x => x.Date)
                .ToList();

            if (revenueByDay.Any())
            {
                Console.WriteLine($"\n📈 DOANH THU 7 NGÀY QUA:");
                foreach (var day in revenueByDay)
                {
                    Console.WriteLine($"- {day.Date:dd/MM}: {day.Revenue:N0}đ");
                }
            }

            // Top món bán chạy
            var topDishes = repository.Dishes.Values
                .OrderByDescending(d => d.SalesCount)
                .Take(5)
                .ToList();

            Console.WriteLine($"\n🏆 TOP 5 MÓN BÁN CHẠY:");
            foreach (var dish in topDishes)
            {
                decimal revenue = dish.Price * dish.SalesCount;
                Console.WriteLine($"- {dish.Name}: {dish.SalesCount} lượt - {revenue:N0}đ");
            }

            Logger.Info("Revenue report generated", "Reports");
            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ShowInventoryReport()
        {
            EnhancedUI.DisplayHeader("BÁO CÁO TỒN KHO");

            var lowStock = repository.Ingredients.Values.Where(ing => ing.IsLowStock).ToList();
            var outOfStock = repository.Ingredients.Values.Where(ing => ing.Quantity == 0).ToList();

            decimal totalValue = repository.Ingredients.Values.Sum(ing => ing.Quantity * ing.PricePerUnit);
            decimal investmentValue = repository.Ingredients.Values.Sum(ing => ing.MinQuantity * ing.PricePerUnit);

            Console.WriteLine("📦 BÁO CÁO TỒN KHO CHI TIẾT:");
            Console.WriteLine($"- Tổng giá trị tồn kho: {totalValue:N0}đ");
            Console.WriteLine($"- Giá trị đầu tư tối thiểu: {investmentValue:N0}đ");
            Console.WriteLine($"- Nguyên liệu cần bổ sung: {lowStock.Count + outOfStock.Count}");

            if (outOfStock.Any())
            {
                Console.WriteLine($"\n🚨 NGUYÊN LIỆU ĐÃ HẾT ({outOfStock.Count}):");
                foreach (var ing in outOfStock)
                {
                    decimal costToRestock = ing.MinQuantity * ing.PricePerUnit;
                    Console.WriteLine($"- {ing.Name}: Cần {ing.MinQuantity} {ing.Unit} - {costToRestock:N0}đ");
                }
            }

            if (lowStock.Any())
            {
                Console.WriteLine($"\n⚠️  NGUYÊN LIỆU SẮP HẾT ({lowStock.Count}):");
                foreach (var ing in lowStock.Take(10))
                {
                    decimal needed = ing.MinQuantity - ing.Quantity;
                    decimal costToRestock = needed * ing.PricePerUnit;
                    Console.WriteLine($"- {ing.Name}: {ing.Quantity}/{ing.MinQuantity} {ing.Unit} - Cần {needed} {ing.Unit} - {costToRestock:N0}đ");
                }
            }

            // Ước tính chi phí bổ sung
            decimal totalRestockCost = outOfStock.Sum(ing => ing.MinQuantity * ing.PricePerUnit) +
                                      lowStock.Sum(ing => (ing.MinQuantity - ing.Quantity) * ing.PricePerUnit);

            Console.WriteLine($"\n💰 ƯỚC TÍNH CHI PHÍ BỔ SUNG: {totalRestockCost:N0}đ");

            Logger.Info("Inventory report generated", "Reports");
            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ShowBusinessEfficiencyReport()
        {
            EnhancedUI.DisplayHeader("BÁO CÁO HIỆU QUẢ KINH DOANH");

            var completedOrders = repository.Orders.Values.Where(o => o.Status == OrderStatus.Completed).ToList();
            var totalRevenue = completedOrders.Sum(o => o.FinalAmount);
            var totalOrders = completedOrders.Count;

            // Tính toán các chỉ số hiệu quả
            decimal avgOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;
            var today = DateTime.Today;
            var ordersToday = completedOrders.Count(o => o.CompletedDate?.Date == today);

            // Phân tích lợi nhuận món ăn
            var profitableDishes = repository.Dishes.Values
                .Where(d => d.Cost > 0 && d.SalesCount > 0)
                .OrderByDescending(d => d.ProfitMargin)
                .Take(5)
                .ToList();

            var lowProfitDishes = repository.Dishes.Values
                .Where(d => d.Cost > 0 && d.SalesCount > 0)
                .OrderBy(d => d.ProfitMargin)
                .Take(5)
                .ToList();

            Console.WriteLine("📈 CHỈ SỐ HIỆU QUẢ KINH DOANH:");
            Console.WriteLine($"- Tổng doanh thu: {totalRevenue:N0}đ");
            Console.WriteLine($"- Tổng số đơn hàng: {totalOrders}");
            Console.WriteLine($"- Giá trị đơn hàng trung bình: {avgOrderValue:N0}đ");
            Console.WriteLine($"- Đơn hàng hôm nay: {ordersToday}");

            if (profitableDishes.Any())
            {
                Console.WriteLine($"\n💎 TOP MÓN CÓ LỢI NHUẬN CAO:");
                foreach (var dish in profitableDishes)
                {
                    Console.WriteLine($"- {dish.Name}: {dish.ProfitMargin:F1}% (Bán được: {dish.SalesCount})");
                }
            }

            if (lowProfitDishes.Any())
            {
                Console.WriteLine($"\n⚡ MÓN CÓ LỢI NHUẬN THẤP (CẦN XEM XÉT):");
                foreach (var dish in lowProfitDishes)
                {
                    Console.WriteLine($"- {dish.Name}: {dish.ProfitMargin:F1}% (Bán được: {dish.SalesCount})");
                }
            }

            // Phân tích hiệu quả combo
            var activeCombos = repository.Combos.Values.Where(c => c.IsActive && c.SalesCount > 0).ToList();
            if (activeCombos.Any())
            {
                Console.WriteLine($"\n🎯 HIỆU QUẢ COMBO:");
                foreach (var combo in activeCombos)
                {
                    combo.CalculateCost(repository.Dishes);
                    Console.WriteLine($"- {combo.Name}: {combo.ProfitMargin:F1}% (Bán được: {combo.SalesCount})");
                }
            }

            Logger.Info("Business efficiency report generated", "Reports");
            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ExportComprehensiveReport()
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"BaoCaoTongHop_{DateTime.Now:yyyyMMddHHmmss}.txt";
                string filePath = Path.Combine(downloadPath, fileName);

                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("BÁO CÁO TỔNG HỢP HỆ THỐNG NHÀ HÀNG");
                    writer.WriteLine($"Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                    writer.WriteLine("==========================================");
                    writer.WriteLine();

                    // Tổng quan hệ thống
                    writer.WriteLine("📊 TỔNG QUAN HỆ THỐNG:");
                    writer.WriteLine($"- Tổng số món ăn: {repository.Dishes.Count}");
                    writer.WriteLine($"- Tổng số nguyên liệu: {repository.Ingredients.Count}");
                    writer.WriteLine($"- Tổng số combo: {repository.Combos.Count(c => c.Value.IsActive)}");
                    writer.WriteLine($"- Tổng số đơn hàng: {repository.Orders.Count}");
                    writer.WriteLine($"- Tổng số người dùng: {repository.Users.Count}");
                    writer.WriteLine();

                    // Doanh thu
                    var completedOrders = repository.Orders.Values.Where(o => o.Status == OrderStatus.Completed).ToList();
                    var totalRevenue = completedOrders.Sum(o => o.FinalAmount);
                    writer.WriteLine("💰 DOANH THU:");
                    writer.WriteLine($"- Tổng doanh thu: {totalRevenue:N0}đ");
                    writer.WriteLine($"- Số đơn hoàn thành: {completedOrders.Count}");
                    writer.WriteLine($"- Giá trị đơn trung bình: {(completedOrders.Any() ? totalRevenue / completedOrders.Count : 0):N0}đ");
                    writer.WriteLine();

                    // Top món bán chạy
                    writer.WriteLine("🏆 TOP 5 MÓN BÁN CHẠY:");
                    var topDishes = repository.Dishes.Values.OrderByDescending(d => d.SalesCount).Take(5);
                    foreach (var dish in topDishes)
                    {
                        writer.WriteLine($"- {dish.Name}: {dish.SalesCount} lượt - {dish.Price * dish.SalesCount:N0}đ");
                    }
                    writer.WriteLine();

                    // Cảnh báo tồn kho
                    var lowStock = repository.Ingredients.Values.Where(ing => ing.IsLowStock).ToList();
                    var outOfStock = repository.Ingredients.Values.Where(ing => ing.Quantity == 0).ToList();
                    writer.WriteLine("⚠️  CẢNH BÁO TỒN KHO:");
                    writer.WriteLine($"- Nguyên liệu sắp hết: {lowStock.Count}");
                    writer.WriteLine($"- Nguyên liệu đã hết: {outOfStock.Count}");
                    if (lowStock.Any() || outOfStock.Any())
                    {
                        writer.WriteLine("  Chi tiết:");
                        foreach (var ing in outOfStock.Concat(lowStock.Take(5)))
                        {
                            string status = ing.Quantity == 0 ? "HẾT" : "SẮP HẾT";
                            writer.WriteLine($"  - {ing.Name}: {ing.Quantity} {ing.Unit} ({status})");
                        }
                    }
                    else
                    {
                        writer.WriteLine("  - Không có cảnh báo");
                    }
                }

                EnhancedUI.DisplaySuccess($"Đã xuất báo cáo tổng hợp: {fileName}");
                repository.AuditLogs.Add(new AuditLog(currentUser.Username, "EXPORT_COMPREHENSIVE_REPORT", "SYSTEM", "", "Xuất báo cáo tổng hợp"));
                Logger.Info($"Comprehensive report exported: {fileName}", "Reports");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to export comprehensive report", "Reports", ex);
                EnhancedUI.DisplayError($"Lỗi khi xuất báo cáo: {ex.Message}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ExportAuditLogs()
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"LichSuThaoTac_{DateTime.Now:yyyyMMddHHmmss}.csv";
                string filePath = Path.Combine(downloadPath, fileName);

                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("Thời gian,Người dùng,Thao tác,Loại thực thể,Mã thực thể,Chi tiết");

                    foreach (var log in repository.AuditLogs.OrderByDescending(a => a.Timestamp))
                    {
                        writer.WriteLine($"{log.Timestamp:dd/MM/yyyy HH:mm},{log.Username},{log.Action},{log.EntityType},{log.EntityId},{log.Details}");
                    }
                }

                EnhancedUI.DisplaySuccess($"Đã xuất lịch sử thao tác: {fileName}");
                Logger.Info($"Audit logs exported: {fileName}", "Reports");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to export audit logs", "Reports", ex);
                EnhancedUI.DisplayError($"Lỗi khi xuất file: {ex.Message}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        // ==================== USER MANAGEMENT METHODS ====================
        private void ShowUserManagementMenu()
        {
            var menuOptions = new List<string>
    {
        "Xem danh sách người dùng",
        "Thêm người dùng mới",
        "Cập nhật người dùng",
        "Xóa người dùng",
        "Xem lịch sử thao tác",
        "Phân quyền người dùng"
    };

            while (true)
            {
                int choice = EnhancedUI.ShowMenu("QUẢN LÝ NGƯỜI DÙNG", menuOptions);
                if (choice == 0) return;

                switch (choice)
                {
                    case 1: DisplayUsers(); break;
                    case 2: AddUser(); break;
                    case 3: UpdateUser(); break;
                    case 4: DeleteUser(); break;
                    case 5: ShowAuditLogs(); break;
                    case 6: ManageUserRoles(); break;
                }
            }
        }

        private void DisplayUsers()
        {
            EnhancedUI.DisplayHeader("DANH SÁCH NGƯỜI DÙNG");

            if (repository.Users.Count == 0)
            {
                EnhancedUI.DisplayInfo("Chưa có người dùng nào trong hệ thống!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                               DANH SÁCH NGƯỜI DÙNG                          ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-15} {1,-25} {2,-12} {3,-15} {4,-10} ║",
                "Tên đăng nhập", "Họ tên", "Vai trò", "Ngày tạo", "Trạng thái");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

            foreach (var user in repository.Users.Values)
            {
                Console.WriteLine("║ {0,-15} {1,-25} {2,-12} {3,-15} {4,-10} ║",
                    user.Username,
                    TruncateString(user.FullName, 25),
                    user.Role,
                    user.CreatedDate.ToString("dd/MM/yyyy"),
                    user.IsActive ? "✅ Hoạt động" : "❌ Vô hiệu");
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void AddUser()
        {
            EnhancedUI.DisplayHeader("THÊM NGƯỜI DÙNG MỚI");

            try
            {
                Console.Write("Tên đăng nhập: ");
                string username = Console.ReadLine();

                if (repository.Users.ContainsKey(username))
                {
                    EnhancedUI.DisplayError("Tên đăng nhập đã tồn tại!");
                    return;
                }

                Console.Write("Họ tên: ");
                string fullName = Console.ReadLine();

                Console.WriteLine("Vai trò:");
                Console.WriteLine("1. Admin - Toàn quyền hệ thống");
                Console.WriteLine("2. Manager - Quản lý nhà hàng");
                Console.WriteLine("3. Staff - Nhân viên phục vụ");
                Console.Write("Chọn: ");

                UserRole role;
                string roleChoice = Console.ReadLine();
                switch (roleChoice)
                {
                    case "1": role = UserRole.Admin; break;
                    case "2": role = UserRole.Manager; break;
                    case "3": role = UserRole.Staff; break;
                    default:
                        EnhancedUI.DisplayWarning("Lựa chọn không hợp lệ, mặc định là Staff!");
                        role = UserRole.Staff;
                        break;
                }

                string password = SecurityService.GenerateRandomPassword();
                string passwordHash = SecurityService.HashPassword(password);

                var user = new User(username, passwordHash, role, fullName);
                repository.Users[username] = user;

                repository.AuditLogs.Add(new AuditLog(currentUser.Username, "ADD_USER", "USER", username, $"Thêm người dùng: {fullName} - {role}"));
                SaveAllData();

                EnhancedUI.DisplaySuccess($"Thêm người dùng thành công!");
                Console.WriteLine($"👤 Tên đăng nhập: {username}");
                Console.WriteLine($"🔑 Mật khẩu mặc định: {password}");
                Console.WriteLine($"⚠️  Hãy yêu cầu người dùng đổi mật khẩu ngay sau khi đăng nhập!");

                Logger.Info($"User {username} added successfully", "UserManagement");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to add user", "UserManagement", ex);
                EnhancedUI.DisplayError($"Lỗi: {ex.Message}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void UpdateUser()
        {
            EnhancedUI.DisplayHeader("CẬP NHẬT NGƯỜI DÙNG");

            DisplayUsers();

            Console.Write("\nNhập tên đăng nhập cần cập nhật: ");
            string username = Console.ReadLine();

            if (!repository.Users.ContainsKey(username))
            {
                EnhancedUI.DisplayError("Người dùng không tồn tại!");
                Console.ReadKey();
                return;
            }

            var user = repository.Users[username];

            // Không cho phép cập nhật chính mình
            if (username == currentUser.Username)
            {
                EnhancedUI.DisplayError("Không thể cập nhật thông tin của chính mình ở đây! Sử dụng chức năng Đổi mật khẩu.");
                Console.ReadKey();
                return;
            }

            try
            {
                Console.WriteLine($"\nCập nhật thông tin người dùng: {user.FullName}");
                Console.WriteLine("(Để trống nếu giữ nguyên)");

                Console.Write($"Họ tên ({user.FullName}): ");
                string fullName = Console.ReadLine();
                if (!string.IsNullOrEmpty(fullName)) user.FullName = fullName;

                Console.WriteLine($"Vai trò hiện tại: {user.Role}");
                Console.WriteLine("1. Admin");
                Console.WriteLine("2. Manager");
                Console.WriteLine("3. Staff");
                Console.Write("Chọn vai trò mới: ");
                string roleChoice = Console.ReadLine();
                if (!string.IsNullOrEmpty(roleChoice))
                {
                    switch (roleChoice)
                    {
                        case "1": user.Role = UserRole.Admin; break;
                        case "2": user.Role = UserRole.Manager; break;
                        case "3": user.Role = UserRole.Staff; break;
                    }
                }

                Console.Write("Trạng thái (1-Hoạt động, 0-Vô hiệu hóa): ");
                string statusChoice = Console.ReadLine();
                if (!string.IsNullOrEmpty(statusChoice))
                {
                    user.IsActive = statusChoice == "1";
                }

                repository.AuditLogs.Add(new AuditLog(currentUser.Username, "UPDATE_USER", "USER", username, $"Cập nhật người dùng: {user.FullName}"));
                SaveAllData();

                EnhancedUI.DisplaySuccess("Cập nhật người dùng thành công!");
                Logger.Info($"User {username} updated", "UserManagement");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to update user {username}", "UserManagement", ex);
                EnhancedUI.DisplayError($"Lỗi: {ex.Message}");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void DeleteUser()
        {
            EnhancedUI.DisplayHeader("XÓA NGƯỜI DÙNG");

            DisplayUsers();

            Console.Write("\nNhập tên đăng nhập cần xóa: ");
            string username = Console.ReadLine();

            if (!repository.Users.ContainsKey(username))
            {
                EnhancedUI.DisplayError("Người dùng không tồn tại!");
                Console.ReadKey();
                return;
            }

            if (username == currentUser.Username)
            {
                EnhancedUI.DisplayError("Không thể xóa chính tài khoản đang đăng nhập!");
                Console.ReadKey();
                return;
            }

            var user = repository.Users[username];

            Console.WriteLine($"\nThông tin người dùng:");
            Console.WriteLine($"- Tên đăng nhập: {user.Username}");
            Console.WriteLine($"- Họ tên: {user.FullName}");
            Console.WriteLine($"- Vai trò: {user.Role}");
            Console.WriteLine($"- Ngày tạo: {user.CreatedDate:dd/MM/yyyy}");

            if (EnhancedUI.Confirm($"Xác nhận xóa người dùng '{user.FullName}'?"))
            {
                repository.Users.Remove(username);
                repository.AuditLogs.Add(new AuditLog(currentUser.Username, "DELETE_USER", "USER", username, $"Xóa người dùng: {user.FullName}"));
                SaveAllData();

                EnhancedUI.DisplaySuccess("Xóa người dùng thành công!");
                Logger.Info($"User {username} deleted", "UserManagement");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ShowAuditLogs()
        {
            EnhancedUI.DisplayHeader("LỊCH SỬ THAO TÁC");

            var recentLogs = repository.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(50)
                .ToList();

            if (!recentLogs.Any())
            {
                EnhancedUI.DisplayInfo("Chưa có lịch sử thao tác!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                               LỊCH SỬ THAO TÁC                               ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-16} {1,-12} {2,-15} {3,-15} {4,-20} ║",
                "Thời gian", "Người dùng", "Thao tác", "Thực thể", "Chi tiết");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

            foreach (var log in recentLogs)
            {
                Console.WriteLine("║ {0,-16} {1,-12} {2,-15} {3,-15} {4,-20} ║",
                    log.Timestamp.ToString("dd/MM HH:mm"),
                    log.Username,
                    log.Action,
                    $"{log.EntityType}:{log.EntityId}",
                    TruncateString(log.Details, 20));
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");

            Console.WriteLine($"\nHiển thị {recentLogs.Count} bản ghi gần nhất");
            Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ManageUserRoles()
        {
            EnhancedUI.DisplayHeader("PHÂN QUYỀN NGƯỜI DÙNG");

            Console.WriteLine("QUYỀN HẠN CÁC VAI TRÒ:");
            Console.WriteLine("👑 Admin - Toàn quyền hệ thống:");
            Console.WriteLine("   - Quản lý người dùng");
            Console.WriteLine("   - Quản lý tất cả dữ liệu");
            Console.WriteLine("   - Truy cập tất cả báo cáo");
            Console.WriteLine("   - Cấu hình hệ thống");

            Console.WriteLine("\n💼 Manager - Quản lý nhà hàng:");
            Console.WriteLine("   - Quản lý menu, nguyên liệu, combo");
            Console.WriteLine("   - Xem báo cáo và thống kê");
            Console.WriteLine("   - Quản lý đơn hàng");
            Console.WriteLine("   - Không quản lý người dùng");

            Console.WriteLine("\n👨‍💼 Staff - Nhân viên phục vụ:");
            Console.WriteLine("   - Tạo và quản lý đơn hàng");
            Console.WriteLine("   - Xem thông tin menu");
            Console.WriteLine("   - Không truy cập báo cáo");
            Console.WriteLine("   - Không quản lý dữ liệu");

            // Thống kê phân quyền
            var adminCount = repository.Users.Values.Count(u => u.Role == UserRole.Admin);
            var managerCount = repository.Users.Values.Count(u => u.Role == UserRole.Manager);
            var staffCount = repository.Users.Values.Count(u => u.Role == UserRole.Staff);

            Console.WriteLine($"\n📊 THỐNG KÊ PHÂN QUYỀN HIỆN TẠI:");
            Console.WriteLine($"- Admin: {adminCount} người");
            Console.WriteLine($"- Manager: {managerCount} người");
            Console.WriteLine($"- Staff: {staffCount} người");

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        // ==================== UTILITY METHODS ====================
        private void ShowUtilityMenu()
        {
            var menuOptions = new List<string>
    {
        "Kiểm tra cảnh báo tồn kho",
        "Tìm kiếm mờ (Fuzzy Search)",
        "Gợi ý món ăn thay thế",
        "Backup dữ liệu",
        "Restore dữ liệu",
        "Xem logs hệ thống",
        "Xuất logs",
        "Dọn dẹp hệ thống"
    };

            while (true)
            {
                int choice = EnhancedUI.ShowMenu("TIỆN ÍCH & CẢNH BÁO", menuOptions);
                if (choice == 0) return;

                switch (choice)
                {
                    case 1: ShowInventoryWarningsDetailed(); break;
                    case 2: FuzzySearch(); break;
                    case 3: SuggestAlternativeDishes(); break;
                    case 4: BackupData(); break;
                    case 5: RestoreData(); break;
                    case 6: ShowSystemLogs(); break;
                    case 7: ExportSystemLogs(); break;
                    case 8: SystemCleanup(); break;
                }
            }
        }

        private void FuzzySearch()
        {
            EnhancedUI.DisplayHeader("TÌM KIẾM MỜ (FUZZY SEARCH)");

            Console.Write("Nhập từ khóa tìm kiếm: ");
            string keyword = Console.ReadLine().ToLower();

            if (string.IsNullOrEmpty(keyword))
            {
                EnhancedUI.DisplayError("Vui lòng nhập từ khóa tìm kiếm!");
                Console.ReadKey();
                return;
            }

            // Tìm kiếm trong món ăn với độ tương đồng
            var dishResults = repository.Dishes.Values
                .Select(d => new { Dish = d, Distance = CalculateLevenshteinDistance(d.Name.ToLower(), keyword) })
                .Where(x => x.Distance <= 3 || x.Dish.Name.ToLower().Contains(keyword))
                .OrderBy(x => x.Distance)
                .Take(10)
                .ToList();

            // Tìm kiếm trong nguyên liệu
            var ingredientResults = repository.Ingredients.Values
                .Select(i => new { Ingredient = i, Distance = CalculateLevenshteinDistance(i.Name.ToLower(), keyword) })
                .Where(x => x.Distance <= 3 || x.Ingredient.Name.ToLower().Contains(keyword))
                .OrderBy(x => x.Distance)
                .Take(10)
                .ToList();


            Console.WriteLine($"\n🔍 KẾT QUẢ TÌM KIẾM CHO '{keyword}':");

            if (dishResults.Any())
            {
                Console.WriteLine("\n🍽️  MÓN ĂN:");
                foreach (var result in dishResults)
                {
                    int similarity = 100 - result.Distance * 25;
                    if (similarity < 0) similarity = 0;

                    Console.WriteLine($"- {result.Dish.Name} (độ tương đồng: {similarity}%) - {result.Dish.Price:N0}đ");
                }
            }

            if (ingredientResults.Any())
            {
                Console.WriteLine("\n🥬 NGUYÊN LIỆU:");
                foreach (var result in ingredientResults)
                {
                    int similarity = 100 - result.Distance * 25;
                    if (similarity < 0) similarity = 0;

                    Console.WriteLine($"- {result.Ingredient.Name} (độ tương đồng: {similarity}%) - {result.Ingredient.Quantity} {result.Ingredient.Unit}");
                }
            }

            if (!dishResults.Any() && !ingredientResults.Any())
            {
                EnhancedUI.DisplayInfo("Không tìm thấy kết quả nào phù hợp!");
            }

            Logger.Info($"Fuzzy search performed for: {keyword}", "Utilities");
            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private int CalculateLevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        private void SuggestAlternativeDishes()
        {
            while (true)
            {
                EnhancedUI.DisplayHeader("🔍 GỢI Ý MÓN ĂN THAY THẾ");

                var dishes = repository.Dishes.Values.ToList();
                const int pageSize = 10;
                int totalPages = (int)Math.Ceiling(dishes.Count / (double)pageSize);
                int currentPage = 1;

                while (true)
                {
                    Console.Clear();
                    EnhancedUI.DisplayHeader("📜 DANH SÁCH MÓN ĂN");

                    var pageItems = dishes.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();

                    Console.WriteLine("╔════════╦════════════════════════════╦════════════╦══════════════════════╗");
                    Console.WriteLine("║  MÃ    ║        TÊN MÓN ĂN         ║   GIÁ (đ)  ║        NHÓM          ║");
                    Console.WriteLine("╠════════╬════════════════════════════╬════════════╬══════════════════════╣");

                    foreach (var dish in pageItems)
                    {
                        Console.WriteLine($"║ {dish.Id,-6} ║ {dish.Name,-26} ║ {dish.Price,10:N0} ║ {dish.Category,-20} ║");
                    }

                    Console.WriteLine("╚════════╩════════════════════════════╩════════════╩══════════════════════╝");
                    Console.WriteLine($"Trang {currentPage}/{totalPages} | Nhập số trang để chuyển, hoặc nhập mã món để xem gợi ý thay thế.");
                    Console.Write("👉 Nhập lựa chọn: ");
                    string input = Console.ReadLine();

                    // Nếu nhập số => đổi trang
                    int pageInput;
                    if (int.TryParse(input, out pageInput))
                    {
                        if (pageInput >= 1 && pageInput <= totalPages)
                        {
                            currentPage = pageInput;
                            continue;
                        }
                        else
                        {
                            EnhancedUI.DisplayWarning("⚠️ Trang không hợp lệ!");
                            continue;
                        }
                    }

                    // Nếu nhập mã món
                    string dishId = input?.Trim();
                    if (!string.IsNullOrEmpty(dishId))
                    {
                        if (!repository.Dishes.ContainsKey(dishId))
                        {
                            EnhancedUI.DisplayError("❌ Món ăn không tồn tại!");
                            Console.ReadKey();
                            break;
                        }

                        var originalDish = repository.Dishes[dishId];

                        // Gợi ý món thay thế cùng nhóm, gần giá, có nguyên liệu
                        var alternatives = repository.Dishes.Values.Where(d =>
                            d.Id != dishId &&
                            d.Category == originalDish.Category &&
                            Math.Abs(d.Price - originalDish.Price) <= originalDish.Price * 0.3m &&
                            CheckDishIngredients(d) &&
                            d.IsAvailable)
                            .Take(5)
                            .ToList();

                        Console.Clear();
                        EnhancedUI.DisplayHeader($"💡 GỢI Ý CHO '{originalDish.Name}'");

                        if (alternatives.Any())
                        {
                            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
                            Console.WriteLine("║     TÊN MÓN ĂN THAY THẾ      │    GIÁ (đ)    │     LỢI NHUẬN    ║");
                            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

                            foreach (var alt in alternatives)
                            {
                                decimal priceDiff = alt.Price - originalDish.Price;
                                string diffText = priceDiff > 0 ? $"(+{priceDiff:N0})" :
                                                 priceDiff < 0 ? $"({priceDiff:N0})" : "Bằng giá";

                                alt.CalculateCost(repository.Ingredients);
                                string profitInfo = alt.Cost > 0 ? $"{alt.ProfitMargin:F1}%" : "N/A";

                                Console.WriteLine($"║ {alt.Name,-30} │ {alt.Price,10:N0} │ {profitInfo,12} {diffText,-10} ║");
                            }

                            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
                        }
                        else
                        {
                            EnhancedUI.DisplayInfo("⚠️ Không có món thay thế phù hợp!");

                            var sameCategory = repository.Dishes.Values.Where(d =>
                                d.Id != dishId &&
                                d.Category == originalDish.Category &&
                                CheckDishIngredients(d) &&
                                d.IsAvailable)
                                .Take(3)
                                .ToList();

                            if (sameCategory.Any())
                            {
                                Console.WriteLine("\n🍽️  MÓN CÙNG NHÓM GỢI Ý:");
                                foreach (var dish in sameCategory)
                                {
                                    Console.WriteLine($"- {dish.Name} ({dish.Price:N0}đ)");
                                }
                            }
                        }

                        Logger.Info($"Alternative dishes suggested for {dishId}", "Utilities");
                        Console.WriteLine("\nNhấn phím bất kỳ để quay lại danh sách...");
                        Console.ReadKey();
                        break;
                    }

                    break;
                }

                Console.WriteLine("\nNhấn phím bất kỳ để quay lại menu chính...");
                Console.ReadKey();
                return;
            }
        }


        private void SystemCleanup()
        {
            EnhancedUI.DisplayHeader("DỌN DẸP HỆ THỐNG");

            Console.WriteLine("Các tác vụ dọn dẹp sẽ được thực hiện:");
            Console.WriteLine("1. Xóa logs hệ thống cũ (trên 7 ngày)");
            Console.WriteLine("2. Thu gom bộ nhớ");
            Console.WriteLine("3. Tối ưu hóa datasets");
            Console.WriteLine("4. Kiểm tra tính toàn vẹn dữ liệu");

            if (EnhancedUI.Confirm("Bắt đầu dọn dẹp hệ thống?"))
            {
                try
                {
                    // Dọn dẹp logs
                    Logger.ClearOldLogs(7);
                    EnhancedUI.DisplaySuccess("Đã dọn dẹp logs cũ");

                    // Thu gom bộ nhớ
                    memoryManager.Cleanup();
                    EnhancedUI.DisplaySuccess("Đã thu gom bộ nhớ");

                    // Tối ưu datasets
                    memoryManager.OptimizeLargeDatasets();
                    EnhancedUI.DisplaySuccess("Đã tối ưu datasets");

                    // Kiểm tra tính toàn vẹn dữ liệu
                    CheckDataIntegrity();
                    EnhancedUI.DisplaySuccess("Đã kiểm tra tính toàn vẹn dữ liệu");

                    Logger.Info("System cleanup completed", "Utilities");
                    EnhancedUI.DisplaySuccess("Dọn dẹp hệ thống hoàn tất!");
                }
                catch (Exception ex)
                {
                    Logger.Error("System cleanup failed", "Utilities", ex);
                    EnhancedUI.DisplayError($"Lỗi trong quá trình dọn dẹp: {ex.Message}");
                }
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void CheckDataIntegrity()
        {
            int issuesFound = 0;

            // Kiểm tra món ăn có nguyên liệu không tồn tại
            foreach (var dish in repository.Dishes.Values)
            {
                foreach (var ingId in dish.Ingredients.Keys)
                {
                    if (!repository.Ingredients.ContainsKey(ingId))
                    {
                        issuesFound++;
                        Logger.Warning($"Dish {dish.Id} references missing ingredient {ingId}", "DataIntegrity");
                    }
                }
            }

            // Kiểm tra combo có món ăn không tồn tại
            foreach (var combo in repository.Combos.Values)
            {
                foreach (var dishId in combo.DishIds)
                {
                    if (!repository.Dishes.ContainsKey(dishId))
                    {
                        issuesFound++;
                        Logger.Warning($"Combo {combo.Id} references missing dish {dishId}", "DataIntegrity");
                    }
                }
            }

            // Kiểm tra đơn hàng có món/combo không tồn tại
            foreach (var order in repository.Orders.Values)
            {
                foreach (var item in order.Items)
                {
                    if (!item.IsCombo && !repository.Dishes.ContainsKey(item.ItemId))
                    {
                        issuesFound++;
                        Logger.Warning($"Order {order.Id} references missing dish {item.ItemId}", "DataIntegrity");
                    }
                    else if (item.IsCombo && !repository.Combos.ContainsKey(item.ItemId))
                    {
                        issuesFound++;
                        Logger.Warning($"Order {order.Id} references missing combo {item.ItemId}", "DataIntegrity");
                    }
                }
            }

            if (issuesFound == 0)
            {
                EnhancedUI.DisplaySuccess("✅ Không phát hiện vấn đề về tính toàn vẹn dữ liệu!");
            }
            else
            {
                EnhancedUI.DisplayWarning($"⚠️  Phát hiện {issuesFound} vấn đề về tính toàn vẹn dữ liệu. Xem logs để biết chi tiết.");
            }
        }

        // ==================== TRUNCATE STRING METHOD ====================
        private string TruncateString(string str, int maxLength)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return str.Length <= maxLength ? str : str.Substring(0, maxLength - 3) + "...";
        }

        // ==================== MAIN METHOD ====================
        public static void Main(string[] args)
        {
            try
            {
                var system = new RestaurantSystem();
                system.Run();
            }
            catch (Exception ex)
            {
                Logger.Error("System crashed", "Main", ex);
                EnhancedUI.DisplayError($"Hệ thống gặp lỗi nghiêm trọng: {ex.Message}");
                Console.WriteLine("Nhấn phím bất kỳ để thoát...");
                Console.ReadKey();
            }
        }
        // ==================== ENHANCED LOGGING FOR DEBUGGING ====================
        private void TestBusinessLogicWithDetailedLogging()
        {
            var system = new RestaurantSystem();
            var repo = system.GetRepository();

            Logger.Info("Starting detailed business logic test", "UnitTests");

            try
            {
                // Log initial state
                Logger.Info($"Initial dishes count: {repo.Dishes.Count}", "UnitTests");
                Logger.Info($"Initial ingredients count: {repo.Ingredients.Count}", "UnitTests");

                // Create test ingredient
                var ingredient = new Ingredient("TEST_ING", "Test Ingredient", "kg", 10, 2, 50000);
                repo.Ingredients["TEST_ING"] = ingredient;
                Logger.Info($"Created test ingredient: {ingredient.Name} - {ingredient.PricePerUnit:N0}đ/{ingredient.Unit}", "UnitTests");

                // Create test dish
                var dish = new Dish("TEST_DISH", "Test Dish", "Test Description", 100000, "Test Category");
                dish.Ingredients["TEST_ING"] = 0.5m;
                repo.Dishes["TEST_DISH"] = dish;

                // Calculate cost
                dish.CalculateCost(repo.Ingredients);
                Logger.Info($"Dish cost calculation: {dish.Cost:N0}đ (Expected: 25,000đ)", "UnitTests");

                // Create combo
                var combo = new Combo("TEST_COMBO", "Test Combo", "Test Description", 10);
                combo.DishIds.Add("TEST_DISH");

                // Calculate combo prices
                combo.CalculateOriginalPrice(repo.Dishes);
                combo.CalculateCost(repo.Dishes);

                Logger.Info($"Combo original price: {combo.OriginalPrice:N0}đ", "UnitTests");
                Logger.Info($"Combo final price: {combo.FinalPrice:N0}đ", "UnitTests");
                Logger.Info($"Combo cost: {combo.Cost:N0}đ", "UnitTests");
                Logger.Info($"Combo profit margin: {combo.ProfitMargin:F2}%", "UnitTests");

                // Verify results
                if (combo.OriginalPrice != 100000)
                    throw new Exception($"Original price mismatch: {combo.OriginalPrice}");

                if (combo.FinalPrice != 90000)
                    throw new Exception($"Final price mismatch: {combo.FinalPrice}");

                Logger.Info("Business logic test completed successfully", "UnitTests");
            }
            catch (Exception ex)
            {
                Logger.Error($"Business logic test failed: {ex.Message}", "UnitTests", ex);
                throw;
            }
        }
    }

    public static class EnhancedUIExtensions
    {
        public static string TruncateString(this string str, int maxLength)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return str.Length <= maxLength ? str : str.Substring(0, maxLength - 3) + "...";
        }
    }


}




