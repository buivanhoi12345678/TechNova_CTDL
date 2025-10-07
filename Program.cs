using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Threading;

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

        public Dish(string id, string name, string description, decimal price, string category)
        {
            Id = id;
            Name = name;
            Description = description;
            Price = price;
            Category = category;
            IsAvailable = true;
            Ingredients = new Dictionary<string, decimal>();
            SalesCount = 0;
        }

        public decimal CalculateCost(Dictionary<string, Ingredient> ingredients)
        {
            decimal cost = 0;
            foreach (var ing in Ingredients)
            {
                if (ingredients.ContainsKey(ing.Key))
                {
                    cost += ingredients[ing.Key].PricePerUnit * ing.Value;
                }
            }
            return cost;
        }
    }

    public class Combo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> DishIds { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal FinalPrice { get { return OriginalPrice * (1 - DiscountPercent / 100); } }
        public DateTime CreatedDate { get; set; }
        public int SalesCount { get; set; }
        public bool IsActive { get; set; }

        public Combo(string id, string name, string description, decimal discountPercent)
        {
            Id = id;
            Name = name;
            Description = description;
            DishIds = new List<string>();
            DiscountPercent = discountPercent;
            CreatedDate = DateTime.Now;
            SalesCount = 0;
            IsActive = true;
        }

        public void CalculateOriginalPrice(Dictionary<string, Dish> dishes)
        {
            OriginalPrice = 0;
            foreach (var dishId in DishIds)
            {
                if (dishes.ContainsKey(dishId))
                {
                    OriginalPrice += dishes[dishId].Price;
                }
            }
        }
    }

    public class OrderItem
    {
        public string ItemId { get; set; }
        public bool IsCombo { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get { return UnitPrice * Quantity; } }
    }

    public class Order
    {
        public string Id { get; set; }
        public string CustomerName { get; set; }
        public List<OrderItem> Items { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public decimal TotalAmount { get { return Items.Sum(item => item.TotalPrice); } }
        public string StaffUsername { get; set; }

        public Order(string id, string customerName, string staffUsername)
        {
            Id = id;
            CustomerName = customerName;
            StaffUsername = staffUsername;
            Items = new List<OrderItem>();
            Status = OrderStatus.Pending;
            OrderDate = DateTime.Now;
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

        public AuditLog(string username, string action, string entityType, string entityId, string details = "")
        {
            Id = Guid.NewGuid().ToString();
            Username = username;
            Action = action;
            EntityType = entityType;
            EntityId = entityId;
            Timestamp = DateTime.Now;
            Details = details;
        }
    }

    // ==================== SERVICES ====================
    public static class SecurityService
    {
        public static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        public static bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }

        public static string GenerateRandomPassword(int length = 8)
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*";
            StringBuilder res = new StringBuilder();
            Random rnd = new Random();

            while (0 < length--)
            {
                res.Append(validChars[rnd.Next(validChars.Length)]);
            }
            return res.ToString();
        }
    }

    public interface ICommand
    {
        void Execute();
        void Undo();
    }

    public class UndoRedoService
    {
        private Stack<ICommand> undoStack = new Stack<ICommand>();
        private Stack<ICommand> redoStack = new Stack<ICommand>();

        public void ExecuteCommand(ICommand command)
        {
            command.Execute();
            undoStack.Push(command);
            redoStack.Clear();
        }

        public void Undo()
        {
            if (undoStack.Count > 0)
            {
                ICommand command = undoStack.Pop();
                command.Undo();
                redoStack.Push(command);
            }
        }

        public void Redo()
        {
            if (redoStack.Count > 0)
            {
                ICommand command = redoStack.Pop();
                command.Execute();
                undoStack.Push(command);
            }
        }

        public bool CanUndo { get { return undoStack.Count > 0; } }
        public bool CanRedo { get { return redoStack.Count > 0; } }
    }

    // ==================== COMMAND PATTERN IMPLEMENTATIONS ====================
    public class AddDishCommand : ICommand
    {
        private RestaurantSystem system;
        private Dish dish;

        public AddDishCommand(RestaurantSystem system, Dish dish)
        {
            this.system = system;
            this.dish = dish;
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

    public class UpdateIngredientCommand : ICommand
    {
        private RestaurantSystem system;
        private Ingredient oldIngredient;
        private Ingredient newIngredient;

        public UpdateIngredientCommand(RestaurantSystem system, Ingredient oldIngredient, Ingredient newIngredient)
        {
            this.system = system;
            this.oldIngredient = oldIngredient;
            this.newIngredient = newIngredient;
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

    public class BatchAddDishesCommand : ICommand
    {
        private RestaurantSystem system;
        private List<Dish> dishes;

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

    // ==================== MAIN SYSTEM ====================
    public class RestaurantSystem
    {
        private Dictionary<string, User> users = new Dictionary<string, User>();
        private Dictionary<string, Ingredient> ingredients = new Dictionary<string, Ingredient>();
        private Dictionary<string, Dish> dishes = new Dictionary<string, Dish>();
        private Dictionary<string, Combo> combos = new Dictionary<string, Combo>();
        private Dictionary<string, Order> orders = new Dictionary<string, Order>();
        private List<AuditLog> auditLogs = new List<AuditLog>();

        private UndoRedoService undoRedoService = new UndoRedoService();
        private User currentUser;
        private bool isRunning;
        private const string DATA_FOLDER = "Data";
        private const string DOWNLOAD_FOLDER = "Downloads";

        // Danh sách nhóm món cố định
        private List<string> dishCategories = new List<string>
        {
            "Món khai vị", "Món chính", "Món phụ", "Tráng miệng", "Đồ uống",
            "Lẩu", "Nướng", "Xào", "Hấp", "Chiên", "Khai vị lạnh", "Salad",
            "Súp", "Món chay", "Hải sản", "Thịt", "Gà", "Bò", "Heo"
        };

        public RestaurantSystem()
        {
            currentUser = null;
            isRunning = true;
            EnsureDataDirectory();
            EnsureDownloadDirectory();
            LoadAllData();
        }

        public Dictionary<string, User> GetUsers() { return users; }
        public Dictionary<string, Ingredient> GetIngredients() { return ingredients; }
        public Dictionary<string, Dish> GetDishes() { return dishes; }
        public Dictionary<string, Combo> GetCombos() { return combos; }
        public Dictionary<string, Order> GetOrders() { return orders; }
        public List<AuditLog> GetAuditLogs() { return auditLogs; }
        public DataRepository GetRepository() { return new DataRepository(users, ingredients, dishes, combos, orders, auditLogs); }

        public void Run()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.Clear();
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
            Console.WriteLine("Cảm ơn bạn đã sử dụng hệ thống!");
        }

        private void DisplayWelcomeScreen()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===============================================");
            Console.WriteLine("    HỆ THỐNG QUẢN LÝ NHÀ HÀNG - RESTAURANT    ");
            Console.WriteLine("===============================================");
            Console.ResetColor();
            Console.WriteLine("\nĐang tải dữ liệu...");
            Thread.Sleep(1000);

            CheckInventoryWarnings();
        }

        private void CheckInventoryWarnings()
        {
            var lowStockIngredients = ingredients.Values.Where(ing => ing.IsLowStock).ToList();

            if (lowStockIngredients.Any())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n⚠️  CẢNH BÁO TỒN KHO:");
                Console.WriteLine("===============================================");
                foreach (var ing in lowStockIngredients)
                {
                    Console.WriteLine($"{ing.Name}: {ing.Quantity} {ing.Unit} (Tối thiểu: {ing.MinQuantity})");
                }
                Console.ResetColor();
                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
            }
        }

        private void ShowLoginScreen()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===============================================");
            Console.WriteLine("               ĐĂNG NHẬP HỆ THỐNG             ");
            Console.WriteLine("===============================================");
            Console.ResetColor();

            Console.Write("Tên đăng nhập: ");
            string username = Console.ReadLine();

            if (username.ToLower() == "x") return;

            Console.Write("Mật khẩu: ");
            string password = ReadPassword();

            if (AuthenticateUser(username, password))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\nĐăng nhập thành công! Chào mừng {currentUser.FullName}");
                Console.ResetColor();
                Thread.Sleep(1000);

                auditLogs.Add(new AuditLog(username, "LOGIN", "SYSTEM", "", "Đăng nhập hệ thống"));
                SaveAllData();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nTên đăng nhập hoặc mật khẩu không đúng!");
                Console.ResetColor();
                Console.WriteLine("Nhấn phím bất kỳ để thử lại...");
                Console.ReadKey();
            }
        }

        private string ReadPassword()
        {
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

        private bool AuthenticateUser(string username, string password)
        {
            if (users.ContainsKey(username))
            {
                var user = users[username];
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
            Console.Clear();
            DisplayHeader();

            Console.WriteLine("╔═════════════════════════════════════════════╗");
            Console.WriteLine("║               MENU CHÍNH                    ║");
            Console.WriteLine("╠═════════════════════════════════════════════╣");
            Console.WriteLine("║ 1. Quản lý món ăn                           ║");
            Console.WriteLine("║ 2. Quản lý nguyên liệu & tồn kho            ║");
            Console.WriteLine("║ 3. Quản lý combo & khuyến mãi               ║");
            Console.WriteLine("║ 4. Bán hàng / đơn đặt món                   ║");
            Console.WriteLine("║ 5. Thống kê & báo cáo                       ║");

            if (currentUser.Role == UserRole.Admin || currentUser.Role == UserRole.Manager)
            {
            Console.WriteLine("║ 6. Quản lý người dùng                       ║");
            }

            Console.WriteLine("║ 7. Tiện ích & cảnh báo                      ║");
            Console.WriteLine("║ 8. Đổi mật khẩu                             ║");
            Console.WriteLine("║ 9. Undo/Redo                                ║");
            Console.WriteLine("║ 0. Đăng xuất                                ║");
            Console.WriteLine("╚═════════════════════════════════════════════╝");
            Console.Write("Chọn chức năng (X để thoát): ");

            string choice = Console.ReadLine();
            if (choice.ToLower() == "x") return;
            ProcessMainMenuChoice(choice);
        }

        private void DisplayHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Người dùng: {currentUser.FullName} ({currentUser.Role})");
            Console.WriteLine($"Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            Console.WriteLine("===============================================");
            Console.ResetColor();
        }

        private void ProcessMainMenuChoice(string choice)
        {
            switch (choice)
            {
                case "1": ShowDishManagementMenu(); break;
                case "2": ShowIngredientManagementMenu(); break;
                case "3": ShowComboManagementMenu(); break;
                case "4": ShowOrderManagementMenu(); break;
                case "5": ShowReportMenu(); break;
                case "6":
                    if (currentUser.Role == UserRole.Admin || currentUser.Role == UserRole.Manager)
                        ShowUserManagementMenu();
                    else
                        ShowAccessDenied();
                    break;
                case "7": ShowUtilityMenu(); break;
                case "8": ChangePassword(); break;
                case "9": ShowUndoRedoMenu(); break;
                case "0": currentUser = null; break;
                default:
                    Console.WriteLine("Lựa chọn không hợp lệ!");
                    Thread.Sleep(1000);
                    break;
            }
        }

        // ==================== INGREDIENT MANAGEMENT ====================
        private void ShowIngredientManagementMenu()
        {
            while (true)
            {
                Console.Clear();
                DisplayHeader();
                Console.WriteLine("╔═════════════════════════════════════════════╗");
                Console.WriteLine("║        QUẢN LÝ NGUYÊN LIỆU & TỒN KHO        ║");
                Console.WriteLine("╠═════════════════════════════════════════════╣");
                Console.WriteLine("║ 1. Xem danh sách nguyên liệu                ║");
                Console.WriteLine("║ 2. Thêm nguyên liệu mới                     ║");
                Console.WriteLine("║ 3. Thêm nguyên liệu từ file                 ║");
                Console.WriteLine("║ 4. Cập nhật nguyên liệu                     ║");
                Console.WriteLine("║ 5. Xóa nguyên liệu                          ║");
                Console.WriteLine("║ 6. Nhập/xuất kho                            ║");
                Console.WriteLine("║ 7. Xem cảnh báo tồn kho                     ║");
                Console.WriteLine("║ 8. Cập nhật nguyên liệu hàng loạt           ║");
                Console.WriteLine("║ 0. Quay lại                                 ║");
                Console.WriteLine("╚═════════════════════════════════════════════╝");
                Console.Write("Chọn chức năng (X để thoát): ");

                string choice = Console.ReadLine();
                if (choice.ToLower() == "x") return;

                switch (choice)
                {
                    case "1": DisplayIngredients(); break;
                    case "2": BatchAddIngredients(); break;
                    case "3": AddIngredientsFromFile(); break;
                    case "4": UpdateIngredient(); break;
                    case "5": BatchDeleteIngredients(); break;
                    case "6": ShowInventoryMenu(); break;
                    case "7": ShowInventoryWarnings(); break;
                    case "8": BatchUpdateIngredients(); break;
                    case "0": return;
                    default: Console.WriteLine("Lựa chọn không hợp lệ!"); Thread.Sleep(1000); break;
                }
            }
        }

        private void DisplayIngredients(int page = 1, int pageSize = 20)
        {
            Console.Clear();
            var ingredientList = ingredients.Values.ToList();
            int totalPages = (int)Math.Ceiling(ingredientList.Count / (double)pageSize);

            if (ingredientList.Count == 0)
            {
                Console.WriteLine("Chưa có nguyên liệu nào trong hệ thống!");
                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                               DANH SÁCH NGUYÊN LIỆU                          ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-8} {1,-25} {2,-10} {3,-8} {4,-10} {5,-12} ║",
                "Mã", "Tên", "Đơn vị", "Số lượng", "Tối thiểu", "Giá/ĐV");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

            var pagedIngredients = ingredientList.Skip((page - 1) * pageSize).Take(pageSize);

            foreach (var ing in pagedIngredients)
            {
                string warning = ing.IsLowStock ? "⚠️" : "";
                Console.WriteLine("║ {0,-8} {1,-25} {2,-10} {3,-8} {4,-10} {5,-12} {6,-2} ║",
                    ing.Id,
                    TruncateString(ing.Name, 25),
                    ing.Unit,
                    ing.Quantity,
                    ing.MinQuantity,
                    $"{ing.PricePerUnit:N0}đ",
                    warning);
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine($"\nTrang {page}/{totalPages} - Tổng cộng: {ingredientList.Count} nguyên liệu");

            if (page > 1) Console.Write("[P] Trang trước | ");
            if (page < totalPages) Console.Write("[N] Trang sau | ");
            Console.WriteLine("[0] Thoát");
            Console.Write("Chọn: ");

            string choice = Console.ReadLine().ToLower();
            if (choice == "n" && page < totalPages)
                DisplayIngredients(page + 1, pageSize);
            else if (choice == "p" && page > 1)
                DisplayIngredients(page - 1, pageSize);
        }


        private void BatchAddIngredients()
        {
            Console.Clear();
            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                             THÊM NGUYÊN LIỆU HÀNG LOẠT                        ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine("(Nhập X để hủy bất kỳ lúc nào, để trống Mã nguyên liệu để kết thúc)");

            try
            {
                int count = 0;
                var addedIngredients = new List<Ingredient>();

                while (true)
                {
                    Console.WriteLine($"\n--- Nguyên liệu thứ {count + 1} ---");

                    Console.Write("Mã nguyên liệu: ");
                    string id = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(id)) break;
                    if (id.ToLower() == "x") return;

                    if (ingredients.ContainsKey(id))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("⚠️  Mã nguyên liệu đã tồn tại! Bỏ qua nguyên liệu này.");
                        Console.ResetColor();
                        continue;
                    }

                    Console.Write("Tên nguyên liệu: ");
                    string name = Console.ReadLine();
                    if (name.ToLower() == "x") return;

                    Console.Write("Đơn vị tính: ");
                    string unit = Console.ReadLine();
                    if (unit.ToLower() == "x") return;

                    Console.Write("Số lượng: ");
                    string quantityInput = Console.ReadLine();
                    if (quantityInput.ToLower() == "x") return;
                    decimal quantity = decimal.Parse(quantityInput);

                    Console.Write("Số lượng tối thiểu: ");
                    string minQuantityInput = Console.ReadLine();
                    if (minQuantityInput.ToLower() == "x") return;
                    decimal minQuantity = decimal.Parse(minQuantityInput);

                    Console.Write("Giá mỗi đơn vị: ");
                    string priceInput = Console.ReadLine();
                    if (priceInput.ToLower() == "x") return;
                    decimal price = decimal.Parse(priceInput);

                    var ingredient = new Ingredient(id, name, unit, quantity, minQuantity, price);
                    ingredients[id] = ingredient;
                    addedIngredients.Add(ingredient);
                    count++;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ Đã thêm nguyên liệu {name}!");
                    Console.ResetColor();
                }

                if (count > 0)
                {
                    // Lưu log
                    auditLogs.Add(new AuditLog(currentUser.Username, "BATCH_ADD_INGREDIENTS", "INGREDIENT", "", $"Thêm {count} nguyên liệu"));
                    SaveAllData();

                    // Hiển thị bảng nguyên liệu vừa thêm
                    Console.Clear();
                    Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
                    Console.WriteLine("║                          DANH SÁCH NGUYÊN LIỆU VỪA THÊM                        ║");
                    Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
                    Console.WriteLine("║ {0,-8} {1,-25} {2,-10} {3,-8} {4,-10} {5,-12} ║",
                        "Mã", "Tên", "Đơn vị", "Số lượng", "Tối thiểu", "Giá/ĐV");
                    Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

                    foreach (var ing in addedIngredients)
                    {
                        string warning = ing.IsLowStock ? "⚠️" : "";
                        Console.WriteLine("║ {0,-8} {1,-25} {2,-10} {3,-8} {4,-10} {5,-12} {6,-2} ║",
                            ing.Id,
                            TruncateString(ing.Name, 25),
                            ing.Unit,
                            ing.Quantity,
                            ing.MinQuantity,
                            $"{ing.PricePerUnit:N0}đ",
                            warning);
                    }

                    Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n🎉 Thêm {count} nguyên liệu thành công!");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\nKhông có nguyên liệu nào được thêm.");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Lỗi: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }




        private void AddIngredientsFromFile()
        {
            Console.Clear();
            Console.WriteLine("THÊM NGUYÊN LIỆU TỪ FILE");
            Console.WriteLine("=========================");

            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                if (!Directory.Exists(downloadPath))
                {
                    Console.WriteLine("Thư mục Downloads không tồn tại!");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"Các file trong thư mục Downloads:");
                var files = Directory.GetFiles(downloadPath, "*.txt").Concat(
                           Directory.GetFiles(downloadPath, "*.csv")).ToArray();

                if (!files.Any())
                {
                    Console.WriteLine("Không tìm thấy file .txt hoặc .csv trong thư mục Downloads!");
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
                    Console.WriteLine("Lựa chọn không hợp lệ!");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ImportIngredientsFromFile(string filePath)
        {
            try
            {
                int successCount = 0;
                int errorCount = 0;

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

                            if (!ingredients.ContainsKey(id))
                            {
                                var ingredient = new Ingredient(id, name, unit, quantity, minQuantity, price);
                                ingredients[id] = ingredient;
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

                auditLogs.Add(new AuditLog(currentUser.Username, "IMPORT_INGREDIENTS", "INGREDIENT", "", $"Nhập từ file: {Path.GetFileName(filePath)}"));
                SaveAllData();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Nhập dữ liệu thành công: {successCount} nguyên liệu");
                if (errorCount > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Có {errorCount} nguyên liệu bị lỗi hoặc trùng mã");
                }
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi khi đọc file: {ex.Message}");
                Console.ResetColor();
            }
        }

        private void UpdateIngredient()
        {
            int page = 1;
            int pageSize = 10;

            while (true)
            {
                Console.Clear();
                Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                           CẬP NHẬT NGUYÊN LIỆU                              ║");
                Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝\n");

                // Lấy danh sách nguyên liệu
                var ingredientList = ingredients.Values.ToList();
                int totalPages = (int)Math.Ceiling(ingredientList.Count / (double)pageSize);

                var pagedIngredients = ingredientList
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Hiển thị bảng
                Console.WriteLine("╔════════╦══════════════════════════╦══════════╦══════════╦══════════════╦════════════╗");
                Console.WriteLine("║   Mã   ║         Tên NL           ║  Đơn vị  ║  SL tồn  ║ SL tối thiểu ║   Giá/ĐV   ║");
                Console.WriteLine("╠════════╬══════════════════════════╬══════════╬══════════╬══════════════╬════════════╣");

                foreach (var ing in pagedIngredients)
                {
                    Console.WriteLine($"║ {ing.Id,-6} ║ {ing.Name,-24} ║ {ing.Unit,-8} ║ {ing.Quantity,-8} ║ {ing.MinQuantity,-12} ║ {ing.PricePerUnit,-10} ║");
                }

                Console.WriteLine("╚════════╩══════════════════════════╩══════════╩══════════╩══════════════╩════════════╝");
                Console.WriteLine($"Trang {page}/{totalPages} - Tổng cộng: {ingredientList.Count} nguyên liệu");
                Console.WriteLine("[Số trang] Nhảy đến trang | [F] Trang đầu | [L] Trang cuối | [Mã NL] Cập nhật | [X] Thoát");

                Console.Write("\nChọn: ");
                string input = Console.ReadLine().Trim();

                if (input.Equals("X", StringComparison.OrdinalIgnoreCase))
                    break;
                else if (input.Equals("F", StringComparison.OrdinalIgnoreCase))
                    page = 1;
                else if (input.Equals("L", StringComparison.OrdinalIgnoreCase))
                    page = totalPages;
                else if (int.TryParse(input, out int pageNum) && pageNum > 0 && pageNum <= totalPages)
                    page = pageNum;
                else if (ingredients.ContainsKey(input))
                {
                    // Cập nhật nguyên liệu
                    var oldIngredient = ingredients[input];
                    var newIngredient = new Ingredient(
                        oldIngredient.Id,
                        oldIngredient.Name,
                        oldIngredient.Unit,
                        oldIngredient.Quantity,
                        oldIngredient.MinQuantity,
                        oldIngredient.PricePerUnit
                    );

                    Console.WriteLine($"\nCập nhật thông tin nguyên liệu [{input}]:\n");

                    Console.Write($"Tên nguyên liệu ({oldIngredient.Name}): ");
                    string name = Console.ReadLine();
                    if (!string.IsNullOrEmpty(name)) newIngredient.Name = name;

                    Console.Write($"Đơn vị tính ({oldIngredient.Unit}): ");
                    string unit = Console.ReadLine();
                    if (!string.IsNullOrEmpty(unit)) newIngredient.Unit = unit;

                    Console.Write($"Số lượng ({oldIngredient.Quantity}): ");
                    string quantityStr = Console.ReadLine();
                    if (!string.IsNullOrEmpty(quantityStr)) newIngredient.Quantity = decimal.Parse(quantityStr);

                    Console.Write($"Số lượng tối thiểu ({oldIngredient.MinQuantity}): ");
                    string minQuantityStr = Console.ReadLine();
                    if (!string.IsNullOrEmpty(minQuantityStr)) newIngredient.MinQuantity = decimal.Parse(minQuantityStr);

                    Console.Write($"Giá mỗi đơn vị ({oldIngredient.PricePerUnit}): ");
                    string priceStr = Console.ReadLine();
                    if (!string.IsNullOrEmpty(priceStr)) newIngredient.PricePerUnit = decimal.Parse(priceStr);

                    var command = new UpdateIngredientCommand(this, oldIngredient, newIngredient);
                    undoRedoService.ExecuteCommand(command);

                    auditLogs.Add(new AuditLog(currentUser.Username, "UPDATE_INGREDIENT", "INGREDIENT", input, $"Cập nhật nguyên liệu: {newIngredient.Name}"));
                    SaveAllData();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n✅ Cập nhật nguyên liệu thành công!");
                    Console.ResetColor();
                    Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
                    Console.ReadKey();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Lựa chọn không hợp lệ!");
                    Console.ResetColor();
                    Console.ReadKey();
                }
            }
        }


        private void BatchUpdateIngredients()
        {
            Console.Clear();
            Console.WriteLine("CẬP NHẬT NGUYÊN LIỆU HÀNG LOẠT");
            Console.WriteLine("================================");
            Console.WriteLine("(Nhập X để hủy bất kỳ lúc nào)");

            try
            {
                Console.WriteLine("1. Cập nhật giá theo phần trăm");
                Console.WriteLine("2. Cập nhật số lượng tồn kho");
                Console.Write("Chọn loại cập nhật: ");

                string choice = Console.ReadLine();
                if (choice.ToLower() == "x") return;

                if (choice == "1")
                {
                    Console.Write("Nhập phần trăm thay đổi giá (+ để tăng, - để giảm): ");
                    string percentInput = Console.ReadLine();
                    if (percentInput.ToLower() == "x") return;
                    decimal percent = decimal.Parse(percentInput);

                    var ingredientsToUpdate = ingredients.Values.ToList();

                    Console.WriteLine($"Sẽ cập nhật {ingredientsToUpdate.Count} nguyên liệu");
                    Console.Write("Xác nhận (y/n): ");
                    string confirm = Console.ReadLine();
                    if (confirm.ToLower() == "y")
                    {
                        foreach (var ingredient in ingredientsToUpdate)
                        {
                            ingredient.PricePerUnit = ingredient.PricePerUnit * (1 + percent / 100);
                        }

                        auditLogs.Add(new AuditLog(currentUser.Username, "BATCH_UPDATE_INGREDIENT_PRICES", "INGREDIENT", "",
                            $"Cập nhật {ingredientsToUpdate.Count} nguyên liệu, thay đổi {percent}%"));
                        SaveAllData();

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Cập nhật giá hàng loạt thành công!");
                        Console.ResetColor();
                    }
                }
                else if (choice == "2")
                {
                    Console.Write("Nhập số lượng cộng thêm (+ để thêm, - để bớt): ");
                    string quantityInput = Console.ReadLine();
                    if (quantityInput.ToLower() == "x") return;
                    decimal quantityChange = decimal.Parse(quantityInput);

                    var ingredientsToUpdate = ingredients.Values.ToList();

                    Console.WriteLine($"Sẽ cập nhật {ingredientsToUpdate.Count} nguyên liệu");
                    Console.Write("Xác nhận (y/n): ");
                    string confirm = Console.ReadLine();
                    if (confirm.ToLower() == "y")
                    {
                        foreach (var ingredient in ingredientsToUpdate)
                        {
                            ingredient.Quantity += quantityChange;
                            if (ingredient.Quantity < 0) ingredient.Quantity = 0;
                        }

                        auditLogs.Add(new AuditLog(currentUser.Username, "BATCH_UPDATE_INGREDIENT_QUANTITIES", "INGREDIENT", "",
                            $"Cập nhật {ingredientsToUpdate.Count} nguyên liệu, thay đổi {quantityChange}"));
                        SaveAllData();

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Cập nhật số lượng hàng loạt thành công!");
                        Console.ResetColor();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi: {ex.Message}");
                Console.ResetColor();
                Console.ReadKey();
            }
        }

        private void BatchDeleteIngredients()
        {
            int page = 1;
            int pageSize = 10;

            while (true)
            {
                Console.Clear();
                Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                        XÓA NGUYÊN LIỆU HÀNG LOẠT                            ║");
                Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝\n");
                Console.WriteLine("(Nhập X để thoát | Nhập F về trang đầu | Nhập L sang trang cuối | Nhập số để nhảy trang)");
                Console.WriteLine("Nhập nhiều mã nguyên liệu (cách nhau dấu phẩy) để xóa.\n");

                // Lấy danh sách nguyên liệu
                var ingredientList = ingredients.Values.ToList();
                int totalPages = (int)Math.Ceiling(ingredientList.Count / (double)pageSize);

                var pagedIngredients = ingredientList
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Hiển thị bảng
                Console.WriteLine("╔════════╦══════════════════════════╦══════════╦══════════╦══════════════╦════════════╗");
                Console.WriteLine("║   Mã   ║         Tên NL           ║  Đơn vị  ║  SL tồn  ║ SL tối thiểu ║   Giá/ĐV   ║");
                Console.WriteLine("╠════════╬══════════════════════════╬══════════╬══════════╬══════════════╬════════════╣");

                foreach (var ing in pagedIngredients)
                {
                    Console.WriteLine($"║ {ing.Id,-6} ║ {ing.Name,-24} ║ {ing.Unit,-8} ║ {ing.Quantity,-8} ║ {ing.MinQuantity,-12} ║ {ing.PricePerUnit,-10} ║");
                }

                Console.WriteLine("╚════════╩══════════════════════════╩══════════╩══════════╩══════════════╩════════════╝");
                Console.WriteLine($"Trang {page}/{totalPages} - Tổng cộng: {ingredientList.Count} nguyên liệu\n");

                Console.Write("Chọn hoặc nhập mã: ");
                string input = Console.ReadLine().Trim();

                if (input.Equals("X", StringComparison.OrdinalIgnoreCase))
                    break;
                else if (input.Equals("F", StringComparison.OrdinalIgnoreCase))
                    page = 1;
                else if (input.Equals("L", StringComparison.OrdinalIgnoreCase))
                    page = totalPages;
                else if (int.TryParse(input, out int pageNum) && pageNum > 0 && pageNum <= totalPages)
                    page = pageNum;
                else
                {
                    // Xử lý xóa danh sách nguyên liệu
                    string[] ingredientIds = input.Split(',')
                                                  .Select(id => id.Trim())
                                                  .Where(id => !string.IsNullOrEmpty(id))
                                                  .ToArray();

                    if (ingredientIds.Length > 0)
                    {
                        int deletedCount = 0;

                        foreach (string ingredientId in ingredientIds)
                        {
                            if (ingredients.ContainsKey(ingredientId))
                            {
                                // Kiểm tra nguyên liệu có đang được dùng trong món nào không
                                bool isUsed = dishes.Values.Any(d => d.Ingredients.ContainsKey(ingredientId));
                                if (!isUsed)
                                {
                                    ingredients.Remove(ingredientId);
                                    deletedCount++;
                                }
                            }
                        }

                        auditLogs.Add(new AuditLog(currentUser.Username, "BATCH_DELETE_INGREDIENTS", "INGREDIENT", "", $"Xóa {deletedCount} nguyên liệu"));
                        SaveAllData();

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"\n✅ Đã xóa {deletedCount} nguyên liệu!");
                        Console.ResetColor();
                        Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
                        Console.ReadKey();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("❌ Không có mã nguyên liệu hợp lệ!");
                        Console.ResetColor();
                        Console.ReadKey();
                    }
                }
            }
        }


        private void BatchUpdateIngredientPrices()
        {
            Console.Write("Nhập phần trăm thay đổi giá (+ để tăng, - để giảm): ");
            decimal percent = decimal.Parse(Console.ReadLine());

            var ingredientsToUpdate = ingredients.Values.ToList();

            Console.WriteLine($"Sẽ cập nhật {ingredientsToUpdate.Count} nguyên liệu");
            Console.Write("Xác nhận (y/n): ");
            if (Console.ReadLine().ToLower() == "y")
            {
                foreach (var ingredient in ingredientsToUpdate)
                {
                    ingredient.PricePerUnit = ingredient.PricePerUnit * (1 + percent / 100);
                }

                auditLogs.Add(new AuditLog(currentUser.Username, "BATCH_UPDATE_INGREDIENT_PRICES", "INGREDIENT", "",
                    $"Cập nhật {ingredientsToUpdate.Count} nguyên liệu, thay đổi {percent}%"));
                SaveAllData();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Cập nhật giá hàng loạt thành công!");
                Console.ResetColor();
            }
        }

        private void BatchUpdateIngredientQuantities()
        {
            Console.Write("Nhập số lượng cộng thêm (+ để thêm, - để bớt): ");
            decimal quantityChange = decimal.Parse(Console.ReadLine());

            var ingredientsToUpdate = ingredients.Values.ToList();

            Console.WriteLine($"Sẽ cập nhật {ingredientsToUpdate.Count} nguyên liệu");
            Console.Write("Xác nhận (y/n): ");
            if (Console.ReadLine().ToLower() == "y")
            {
                foreach (var ingredient in ingredientsToUpdate)
                {
                    ingredient.Quantity += quantityChange;
                    if (ingredient.Quantity < 0) ingredient.Quantity = 0;
                }

                auditLogs.Add(new AuditLog(currentUser.Username, "BATCH_UPDATE_INGREDIENT_QUANTITIES", "INGREDIENT", "",
                    $"Cập nhật {ingredientsToUpdate.Count} nguyên liệu, thay đổi {quantityChange}"));
                SaveAllData();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Cập nhật số lượng hàng loạt thành công!");
                Console.ResetColor();
            }
        }


        private void DisplayIngredientsSimple(int page = 1, int pageSize = 20)
        {
            Console.Clear();
            var ingredientList = ingredients.Values.ToList();
            int totalPages = (int)Math.Ceiling(ingredientList.Count / (double)pageSize);

            if (ingredientList.Count == 0)
            {
                Console.WriteLine("Chưa có nguyên liệu nào trong hệ thống!");
                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                               DANH SÁCH NGUYÊN LIỆU                          ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-8} {1,-25} {2,-10} {3,-8} {4,-10} {5,-12} ║",
                "Mã", "Tên", "Đơn vị", "Số lượng", "Tối thiểu", "Trạng thái");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

            var pagedIngredients = ingredientList.Skip((page - 1) * pageSize).Take(pageSize);

            foreach (var ing in pagedIngredients)
            {
                string status = ing.IsLowStock ? "⚠️ Sắp hết" : "✅ Đủ";
                Console.WriteLine("║ {0,-8} {1,-25} {2,-10} {3,-8} {4,-10} {5,-12} ║",
                    ing.Id,
                    TruncateString(ing.Name, 25),
                    ing.Unit,
                    ing.Quantity,
                    ing.MinQuantity,
                    status);
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine($"\nTrang {page}/{totalPages} - Tổng cộng: {ingredientList.Count} nguyên liệu");

            if (totalPages > 1)
            {
                if (page > 1) Console.Write("[P] Trang trước | ");
                if (page < totalPages) Console.Write("[N] Trang sau | ");
                Console.WriteLine("[0] Thoát");
                Console.Write("Chọn: ");

                string choice = Console.ReadLine().ToLower();
                if (choice == "n" && page < totalPages)
                    DisplayIngredientsSimple(page + 1, pageSize);
                else if (choice == "p" && page > 1)
                    DisplayIngredientsSimple(page - 1, pageSize);
            }
            else
            {
                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
            }
        }

        private void ShowInventoryMenu()
        {
            int page = 1;
            int pageSize = 10;

            while (true)
            {
                Console.Clear();
                Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                              QUẢN LÝ KHO HÀNG                                ║");
                Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝\n");
                Console.WriteLine("(Nhập T để thoát | Nhập F về trang đầu | Nhập L sang trang cuối | Nhập số để nhảy trang)\n");

                var ingredientList = ingredients.Values.ToList();
                int totalPages = (int)Math.Ceiling(ingredientList.Count / (double)pageSize);

                var pagedIngredients = ingredientList
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Hiển thị bảng nguyên liệu
                Console.WriteLine("╔════════╦══════════════════════════╦══════════╦══════════╦══════════════╦════════════╗");
                Console.WriteLine("║   Mã   ║         Tên NL           ║  Đơn vị  ║  SL tồn  ║ SL tối thiểu ║   Giá/ĐV   ║");
                Console.WriteLine("╠════════╬══════════════════════════╬══════════╬══════════╬══════════════╬════════════╣");

                foreach (var ing in pagedIngredients)
                {
                    Console.WriteLine($"║ {ing.Id,-6} ║ {ing.Name,-24} ║ {ing.Unit,-8} ║ {ing.Quantity,-8} ║ {ing.MinQuantity,-12} ║ {ing.PricePerUnit,-10} ║");
                }

                Console.WriteLine("╚════════╩══════════════════════════╩══════════╩══════════╩══════════════╩════════════╝");
                Console.WriteLine($"Trang {page}/{totalPages} - Tổng cộng: {ingredientList.Count} nguyên liệu\n");

                Console.WriteLine("N. Nhập kho");
                Console.WriteLine("X. Xuất kho");
                Console.Write("Chọn: ");
                string choice = Console.ReadLine().Trim();

                if (choice.Equals("T", StringComparison.OrdinalIgnoreCase))
                    break;
                else if (choice.Equals("F", StringComparison.OrdinalIgnoreCase))
                    page = 1;
                else if (choice.Equals("L", StringComparison.OrdinalIgnoreCase))
                    page = totalPages;
                else if (int.TryParse(choice, out int pageNum) && pageNum > 0 && pageNum <= totalPages)
                    page = pageNum;
                else if (choice == "N")
                    ImportInventory();
                else if (choice == "X")
                    ExportInventory();
            }
        }


        private void ImportInventory()
        {
            Console.Write("\nMã nguyên liệu: ");
            string id = Console.ReadLine()?.Trim();

            // Kiểm tra ID có tồn tại không
            if (string.IsNullOrEmpty(id) || !ingredients.ContainsKey(id))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Nguyên liệu không tồn tại!");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }

            Console.Write("Số lượng nhập: ");
            string input = Console.ReadLine();

            // Dùng TryParse để tránh FormatException
            if (!decimal.TryParse(input, out decimal quantity) || quantity <= 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Số lượng không hợp lệ. Vui lòng nhập số dương!");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }

            // Cập nhật số lượng tồn kho
            ingredients[id].Quantity += quantity;
            ingredients[id].LastUpdated = DateTime.Now;

            // Lưu log thao tác
            auditLogs.Add(new AuditLog(currentUser.Username, "IMPORT_INVENTORY", "INGREDIENT", id, $"Nhập kho: +{quantity}"));
            SaveAllData();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Nhập kho thành công!");
            Console.ResetColor();
            Console.ReadKey();
        }

        private void ExportInventory()
        {
            Console.Write("\nMã nguyên liệu: ");
            string id = Console.ReadLine();

            if (!ingredients.ContainsKey(id))
            {
                Console.WriteLine("Nguyên liệu không tồn tại!");
                Console.ReadKey();
                return;
            }

            Console.Write("Số lượng xuất: ");
            decimal quantity = decimal.Parse(Console.ReadLine());

            if (ingredients[id].Quantity < quantity)
            {
                Console.WriteLine("Số lượng xuất vượt quá tồn kho!");
                Console.ReadKey();
                return;
            }

            ingredients[id].Quantity -= quantity;
            ingredients[id].LastUpdated = DateTime.Now;

            auditLogs.Add(new AuditLog(currentUser.Username, "EXPORT_INVENTORY", "INGREDIENT", id, $"Xuất kho: -{quantity}"));
            SaveAllData();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Xuất kho thành công!");
            Console.ResetColor();
            Console.ReadKey();
        }

        private void ShowInventoryWarnings()
        {
            Console.Clear();
            Console.WriteLine("CẢNH BÁO TỒN KHO");
            Console.WriteLine("================");

            var lowStockIngredients = ingredients.Values.Where(ing => ing.IsLowStock).ToList();

            if (!lowStockIngredients.Any())
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ Không có nguyên liệu nào sắp hết!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠️  Có {lowStockIngredients.Count} nguyên liệu sắp hết:");
                Console.ResetColor();

                Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                     NGUYÊN LIỆU SẮP HẾT                       ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
                Console.WriteLine("║ {0,-25} {1,-15} {2,-12} {3,-10} ║",
                    "Tên nguyên liệu", "Tồn kho", "Tối thiểu", "Chênh lệch");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

                foreach (var ing in lowStockIngredients)
                {
                    decimal difference = ing.Quantity - ing.MinQuantity;
                    Console.WriteLine("║ {0,-25} {1,-15} {2,-12} {3,-10} ║",
                        TruncateString(ing.Name, 25),
                        $"{ing.Quantity} {ing.Unit}",
                        $"{ing.MinQuantity} {ing.Unit}",
                        $"{difference} {ing.Unit}");
                }
                Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

                // Xuất file cảnh báo
                ExportInventoryWarningFile(lowStockIngredients);
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ExportInventoryWarningFile(List<Ingredient> lowStockIngredients)
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"CanhBaoTonKho_{DateTime.Now:yyyyMMddHHmmss}.txt";
                string filePath = Path.Combine(downloadPath, fileName);

                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("BÁO CÁO CẢNH BÁO TỒN KHO");
                    writer.WriteLine($"Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                    writer.WriteLine("==========================================");
                    writer.WriteLine();

                    foreach (var ing in lowStockIngredients)
                    {
                        writer.WriteLine($"{ing.Name}: {ing.Quantity} {ing.Unit} (Tối thiểu: {ing.MinQuantity} {ing.Unit})");
                    }
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\nĐã xuất file cảnh báo: {fileName}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi khi xuất file: {ex.Message}");
                Console.ResetColor();
            }
        }

       // ==================== DISH MANAGEMENT ====================
        private void ShowDishManagementMenu()
        {
            while (true)
            {
                Console.Clear();
                DisplayHeader();
                Console.WriteLine("╔═════════════════════════════════════════════╗");
                Console.WriteLine("║           QUẢN LÝ MÓN ĂN                    ║");
                Console.WriteLine("╠═════════════════════════════════════════════╣");
                Console.WriteLine("║ 1. Xem danh sách món ăn                     ║");
                Console.WriteLine("║ 2. Thêm món ăn mới                          ║");
                Console.WriteLine("║ 3. Thêm món ăn từ file                      ║");
                Console.WriteLine("║ 4. Cập nhật món ăn                          ║");
                Console.WriteLine("║ 5. Xóa món ăn                               ║");
                Console.WriteLine("║ 6. Tìm kiếm món ăn                          ║");
                Console.WriteLine("║ 7. Lọc món ăn                               ║");
                Console.WriteLine("║ 8. Xem chi tiết món ăn                      ║");
                Console.WriteLine("║ 9. Cập nhật món ăn hàng loạt                ║");
                Console.WriteLine("║ 0. Quay lại                                 ║");
                Console.WriteLine("╚═════════════════════════════════════════════╝");
                Console.Write("Chọn chức năng (X để thoát): ");

                string choice = Console.ReadLine();
                if (choice.ToLower() == "x") return;

                switch (choice)
                {
                    case "1": DisplayDishes(); break;
                    case "2": BatchAddDishes(); break;
                    case "3": AddDishesFromFile(); break;
                    case "4": UpdateDish(); break;
                    case "5": BatchDeleteDishes(); break;
                    case "6": SearchDishes(); break;
                    case "7": FilterDishes(); break;
                    case "8": ShowDishDetail(); break;
                    case "10": BatchUpdateDishes(); break;
                    case "0": return;
                    default: Console.WriteLine("Lựa chọn không hợp lệ!"); Thread.Sleep(1000); break;
                }
            }
        }

        private void DisplayDishes(int page = 1, int pageSize = 20)
        {
            Console.Clear();
            var dishList = dishes.Values.ToList();
            int totalPages = (int)Math.Ceiling(dishList.Count / (double)pageSize);

            if (dishList.Count == 0)
            {
                Console.WriteLine("Chưa có món ăn nào trong hệ thống!");
                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                               DANH SÁCH MÓN ĂN                               ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-8} {1,-25} {2,-15} {3,-12} {4,-10} ║", "Mã", "Tên món", "Nhóm", "Giá", "Tình trạng");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

            var pagedDishes = dishList.Skip((page - 1) * pageSize).Take(pageSize);

            foreach (var dish in pagedDishes)
            {
                string status = dish.IsAvailable ? "Có sẵn" : "Hết hàng";
                Console.WriteLine("║ {0,-8} {1,-25} {2,-15} {3,-12} {4,-10} ║",
                    dish.Id,
                    TruncateString(dish.Name, 25),
                    TruncateString(dish.Category, 15),
                    $"{dish.Price:N0}đ",
                    status);
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine($"\nTrang {page}/{totalPages} - Tổng cộng: {dishList.Count} món");

            if (page > 1) Console.Write("[P] Trang trước | ");
            if (page < totalPages) Console.Write("[N] Trang sau | ");
            Console.WriteLine("[0] Thoát");
            Console.Write("Chọn: ");

            string choice = Console.ReadLine().ToLower();
            if (choice == "n" && page < totalPages)
                DisplayDishes(page + 1, pageSize);
            else if (choice == "p" && page > 1)
                DisplayDishes(page - 1, pageSize);
        }


        private void BatchAddDishes()
        {
            Console.Clear();
            Console.WriteLine("THÊM MÓN ĂN HÀNG LOẠT");
            Console.WriteLine("======================");
            Console.WriteLine("(Nhập X để hủy bất kỳ lúc nào)");

            try
            {
                List<Dish> newDishes = new List<Dish>();
                int count = 0;

                Console.WriteLine("Nhập thông tin món ăn (để trống mã món để kết thúc):");

                while (true)
                {
                    Console.WriteLine($"\n--- Món ăn thứ {count + 1} ---");

                    Console.Write("Mã món ăn: ");
                    string id = Console.ReadLine();
                    if (id.ToLower() == "x") return;
                    if (string.IsNullOrEmpty(id)) break;

                    if (dishes.ContainsKey(id))
                    {
                        Console.WriteLine("Mã món ăn đã tồn tại! Bỏ qua món này.");
                        continue;
                    }

                    Console.Write("Tên món ăn: ");
                    string name = Console.ReadLine();
                    if (name.ToLower() == "x") return;

                    Console.Write("Mô tả: ");
                    string description = Console.ReadLine();
                    if (description.ToLower() == "x") return;

                    Console.Write("Giá: ");
                    string priceInput = Console.ReadLine();
                    if (priceInput.ToLower() == "x") return;
                    decimal price = decimal.Parse(priceInput);

                    // Chọn nhóm món
                    Console.WriteLine("\nChọn nhóm món:");
                    for (int i = 0; i < dishCategories.Count; i++)
                    {
                        Console.WriteLine($"{i + 1}. {dishCategories[i]}");
                    }
                    Console.Write("Chọn số (hoặc nhập nhóm mới): ");
                    string categoryChoice = Console.ReadLine();
                    if (categoryChoice.ToLower() == "x") return;

                    string category;
                    if (int.TryParse(categoryChoice, out int index) && index > 0 && index <= dishCategories.Count)
                    {
                        category = dishCategories[index - 1];
                    }
                    else
                    {
                        category = categoryChoice;
                    }

                    var dish = new Dish(id, name, description, price, category);
                    newDishes.Add(dish);
                    count++;

                    Console.WriteLine($"Đã thêm món {name} vào danh sách chờ!");
                }

                if (newDishes.Count > 0)
                {
                    Console.WriteLine($"\nSẽ thêm {newDishes.Count} món ăn:");
                    foreach (var dish in newDishes)
                    {
                        Console.WriteLine($"- {dish.Id}: {dish.Name} - {dish.Price:N0}đ");
                    }

                    Console.Write("Xác nhận thêm (y/n): ");
                    string confirm = Console.ReadLine();
                    if (confirm.ToLower() == "y")
                    {
                        var command = new BatchAddDishesCommand(this, newDishes);
                        undoRedoService.ExecuteCommand(command);

                        auditLogs.Add(new AuditLog(currentUser.Username, "BATCH_ADD_DISHES", "DISH", "", $"Thêm {newDishes.Count} món"));
                        SaveAllData();

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Thêm {newDishes.Count} món ăn thành công!");
                        Console.ResetColor();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }


        private void AddDishesFromFile()
        {
            Console.Clear();
            Console.WriteLine("THÊM MÓN ĂN TỪ FILE");
            Console.WriteLine("====================");

            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                if (!Directory.Exists(downloadPath))
                {
                    Console.WriteLine("Thư mục Downloads không tồn tại!");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"Các file trong thư mục Downloads:");
                var files = Directory.GetFiles(downloadPath, "*.txt").Concat(
                           Directory.GetFiles(downloadPath, "*.csv")).ToArray();

                if (!files.Any())
                {
                    Console.WriteLine("Không tìm thấy file .txt hoặc .csv trong thư mục Downloads!");
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
                    Console.WriteLine("Lựa chọn không hợp lệ!");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ImportDishesFromFile(string filePath)
        {
            try
            {
                int successCount = 0;
                int errorCount = 0;

                string[] lines = File.ReadAllLines(filePath);
                foreach (string line in lines.Skip(1)) // Bỏ qua header
                {
                    try
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length >= 4)
                        {
                            string id = parts[0].Trim();
                            string name = parts[1].Trim();
                            string description = parts[2].Trim();
                            decimal price = decimal.Parse(parts[3].Trim());
                            string category = parts.Length > 4 ? parts[4].Trim() : "Món chính";

                            if (!dishes.ContainsKey(id))
                            {
                                var dish = new Dish(id, name, description, price, category);
                                dishes[id] = dish;
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

                auditLogs.Add(new AuditLog(currentUser.Username, "IMPORT_DISHES", "DISH", "", $"Nhập từ file: {Path.GetFileName(filePath)}"));
                SaveAllData();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Nhập dữ liệu thành công: {successCount} món");
                if (errorCount > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Có {errorCount} món bị lỗi hoặc trùng mã");
                }
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi khi đọc file: {ex.Message}");
                Console.ResetColor();
            }
        }

        private void UpdateDish()
        {
            int page = 1;
            int pageSize = 10;
            int totalPages = (int)Math.Ceiling(dishes.Count / (double)pageSize);

            while (true)
            {
                Console.Clear();
                Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                               CẬP NHẬT MÓN ĂN                                  ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
                Console.WriteLine("║ Mã       Tên món                   Nhóm            Giá          Tình trạng     ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

                var dishList = dishes.Values.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                foreach (var d in dishList) // ⚡ đổi tên biến ở đây
                {
                    Console.WriteLine($"║ {d.Id,-8} {d.Name,-25} {d.Category,-15} {d.Price,10:N0}đ   Có sẵn      ║");
                }

                Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
                Console.WriteLine($"Trang {page}/{totalPages} - Tổng cộng: {dishes.Count} món");
                Console.WriteLine("[Số trang] Chuyển đến trang | [Mã món] Cập nhật món | [0] Thoát");
                Console.Write("Chọn: ");
                string choice = Console.ReadLine();

                if (choice == "0") break;

                if (int.TryParse(choice, out int pageChoice) && pageChoice > 0 && pageChoice <= totalPages)
                {
                    page = pageChoice;
                    continue;
                }

                // === Cập nhật món ăn theo mã ===
                if (!dishes.ContainsKey(choice))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Món ăn không tồn tại!");
                    Console.ResetColor();
                    Console.ReadKey();
                    continue;
                }

                var dish = dishes[choice];

                Console.Write($"Tên món ăn ({dish.Name}): ");
                string name = Console.ReadLine();
                if (!string.IsNullOrEmpty(name)) dish.Name = name;

                Console.Write($"Mô tả ({dish.Description}): ");
                string description = Console.ReadLine();
                if (!string.IsNullOrEmpty(description)) dish.Description = description;

                Console.Write($"Giá ({dish.Price}): ");
                string priceStr = Console.ReadLine();
                if (!string.IsNullOrEmpty(priceStr)) dish.Price = decimal.Parse(priceStr);

                Console.WriteLine($"\nNhóm món hiện tại: {dish.Category}");
                Console.WriteLine("Chọn nhóm món mới:");
                for (int i = 0; i < dishCategories.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {dishCategories[i]}");
                }
                Console.Write("Chọn số (để trống nếu giữ nguyên): ");
                string categoryChoice = Console.ReadLine();

                if (!string.IsNullOrEmpty(categoryChoice))
                {
                    if (int.TryParse(categoryChoice, out int index) && index > 0 && index <= dishCategories.Count)
                        dish.Category = dishCategories[index - 1];
                    else
                        dish.Category = categoryChoice;
                }

                auditLogs.Add(new AuditLog(currentUser.Username, "UPDATE_DISH", "DISH", choice, $"Cập nhật món: {dish.Name}"));
                SaveAllData();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ Cập nhật món ăn thành công!");
                Console.ResetColor();
                Console.ReadKey();
            }
        }


        private void BatchUpdateDishes()
        {
            Console.Clear();
            Console.WriteLine("CẬP NHẬT MÓN ĂN HÀNG LOẠT");
            Console.WriteLine("===========================");
            Console.WriteLine("(Nhập X để hủy bất kỳ lúc nào)");

            try
            {
                Console.WriteLine("1. Cập nhật giá theo phần trăm");
                Console.WriteLine("2. Cập nhật trạng thái sẵn có");
                Console.WriteLine("3. Cập nhật theo điều kiện");
                Console.Write("Chọn loại cập nhật: ");

                string choice = Console.ReadLine();
                if (choice.ToLower() == "x") return;

                List<Dish> dishesToUpdate = new List<Dish>();
                List<Dish> oldDishesState = new List<Dish>();

                if (choice == "1")
                {
                    Console.Write("Nhập phần trăm thay đổi giá (+ để tăng, - để giảm): ");
                    string percentInput = Console.ReadLine();
                    if (percentInput.ToLower() == "x") return;
                    decimal percent = decimal.Parse(percentInput);

                    Console.Write("Áp dụng cho nhóm món (để trống nếu áp dụng cho tất cả): ");
                    string categoryFilter = Console.ReadLine();
                    if (categoryFilter.ToLower() == "x") return;

                    dishesToUpdate = dishes.Values.Where(d =>
                        string.IsNullOrEmpty(categoryFilter) || d.Category == categoryFilter).ToList();

                    // Lưu trạng thái cũ
                    foreach (var dish in dishesToUpdate)
                    {
                        oldDishesState.Add(new Dish(dish.Id, dish.Name, dish.Description, dish.Price, dish.Category)
                        {
                            IsAvailable = dish.IsAvailable,
                            Ingredients = new Dictionary<string, decimal>(dish.Ingredients),
                            SalesCount = dish.SalesCount
                        });
                    }

                    Console.WriteLine($"Sẽ cập nhật {dishesToUpdate.Count} món ăn");
                    Console.Write("Xác nhận (y/n): ");
                    string confirm = Console.ReadLine();
                    if (confirm.ToLower() == "y")
                    {
                        foreach (var dish in dishesToUpdate)
                        {
                            dish.Price = dish.Price * (1 + percent / 100);
                        }

                        var command = new BatchUpdateDishesCommand(this, oldDishesState, dishesToUpdate);
                        undoRedoService.ExecuteCommand(command);

                        auditLogs.Add(new AuditLog(currentUser.Username, "BATCH_UPDATE_DISHES", "DISH", "",
                            $"Cập nhật {dishesToUpdate.Count} món, thay đổi {percent}%"));
                        SaveAllData();

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Cập nhật giá hàng loạt thành công!");
                        Console.ResetColor();
                    }
                }
                else if (choice == "2")
                {
                    Console.Write("Đặt trạng thái sẵn có (1-Có sẵn, 0-Hết hàng): ");
                    string statusInput = Console.ReadLine();
                    if (statusInput.ToLower() == "x") return;
                    bool isAvailable = statusInput == "1";

                    Console.Write("Áp dụng cho nhóm món (để trống nếu áp dụng cho tất cả): ");
                    string categoryFilter = Console.ReadLine();
                    if (categoryFilter.ToLower() == "x") return;

                    dishesToUpdate = dishes.Values.Where(d =>
                        string.IsNullOrEmpty(categoryFilter) || d.Category == categoryFilter).ToList();

                    // Lưu trạng thái cũ
                    foreach (var dish in dishesToUpdate)
                    {
                        oldDishesState.Add(new Dish(dish.Id, dish.Name, dish.Description, dish.Price, dish.Category)
                        {
                            IsAvailable = dish.IsAvailable,
                            Ingredients = new Dictionary<string, decimal>(dish.Ingredients),
                            SalesCount = dish.SalesCount
                        });
                    }

                    Console.WriteLine($"Sẽ cập nhật {dishesToUpdate.Count} món ăn");
                    Console.Write("Xác nhận (y/n): ");
                    string confirm = Console.ReadLine();
                    if (confirm.ToLower() == "y")
                    {
                        foreach (var dish in dishesToUpdate)
                        {
                            dish.IsAvailable = isAvailable;
                        }

                        var command = new BatchUpdateDishesCommand(this, oldDishesState, dishesToUpdate);
                        undoRedoService.ExecuteCommand(command);

                        auditLogs.Add(new AuditLog(currentUser.Username, "BATCH_UPDATE_DISHES", "DISH", "",
                            $"Cập nhật {dishesToUpdate.Count} món, trạng thái: {(isAvailable ? "Có sẵn" : "Hết hàng")}"));
                        SaveAllData();

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Cập nhật trạng thái hàng loạt thành công!");
                        Console.ResetColor();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi: {ex.Message}");
                Console.ResetColor();
                Console.ReadKey();
            }
        }

        private void BatchDeleteDishes(int page = 1, int pageSize = 20)
        {
            while (true)
            {
                Console.Clear();
                var dishList = dishes.Values.ToList();
                int totalPages = (int)Math.Ceiling(dishList.Count / (double)pageSize);

                if (dishList.Count == 0)
                {
                    Console.WriteLine("❌ Không có món ăn nào trong hệ thống!");
                    Console.WriteLine("Nhấn phím bất kỳ để quay lại...");
                    Console.ReadKey();
                    return;
                }

                // Vẽ bảng
                Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                               DANH SÁCH MÓN ĂN                                ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
                Console.WriteLine("║ {0,-8} {1,-25} {2,-15} {3,-12} {4,-10} ║", "Mã", "Tên món", "Nhóm", "Giá", "Tình trạng");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

                var pagedDishes = dishList.Skip((page - 1) * pageSize).Take(pageSize);

                foreach (var dish in pagedDishes)
                {
                    string status = dish.IsAvailable ? "Có sẵn" : "Hết hàng";
                    Console.WriteLine("║ {0,-8} {1,-25} {2,-15} {3,-12} {4,-10} ║",
                        dish.Id,
                        TruncateString(dish.Name, 25),
                        TruncateString(dish.Category, 15),
                        $"{dish.Price:N0}đ",
                        status);
                }

                Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
                Console.WriteLine($"\nTrang {page}/{totalPages} - Tổng cộng: {dishList.Count} món");

                Console.WriteLine("Nhập số trang để chuyển | [F] về trang đầu | [L] trang cuối | [X] thoát");
                Console.Write("Hoặc nhập danh sách mã món cần xóa (cách nhau bởi dấu phẩy): ");
                string input = Console.ReadLine().Trim();

                if (string.IsNullOrEmpty(input)) continue;

                string choice = input.ToLower();

                if (choice == "x") return;
                if (choice == "f") { page = 1; continue; }
                if (choice == "l") { page = totalPages; continue; }

                // Nếu nhập số -> nhảy đến trang
                if (int.TryParse(input, out int gotoPage))
                {
                    if (gotoPage >= 1 && gotoPage <= totalPages)
                    {
                        page = gotoPage;
                        continue;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"⚠️ Trang không hợp lệ! (1 - {totalPages})");
                        Console.ResetColor();
                        Console.ReadKey();
                        continue;
                    }
                }

                // Nếu nhập mã món -> xoá
                string[] dishIds = input.Split(',').Select(id => id.Trim()).ToArray();
                List<Dish> dishesToDelete = new List<Dish>();
                List<string> notFound = new List<string>();

                foreach (string dishId in dishIds)
                {
                    if (dishes.ContainsKey(dishId))
                        dishesToDelete.Add(dishes[dishId]);
                    else
                        notFound.Add(dishId);
                }

                if (dishesToDelete.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Không tìm thấy món ăn nào khớp!");
                    Console.ResetColor();
                    Console.ReadKey();
                    continue;
                }

                Console.WriteLine($"\nSẽ xóa {dishesToDelete.Count} món ăn:");
                foreach (var dish in dishesToDelete)
                {
                    Console.WriteLine($"- {dish.Id}: {dish.Name}");
                }
                if (notFound.Count > 0)
                {
                    Console.WriteLine($"⚠️ Không tìm thấy: {string.Join(", ", notFound)}");
                }

                Console.Write("Xác nhận xóa (y/n): ");
                string confirm = Console.ReadLine().Trim().ToLower();
                if (confirm == "y")
                {
                    foreach (var dish in dishesToDelete)
                    {
                        dishes.Remove(dish.Id);
                    }

                    auditLogs.Add(new AuditLog(currentUser.Username, "BATCH_DELETE_DISHES", "DISH", "", $"Xóa {dishesToDelete.Count} món"));
                    SaveAllData();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ Đã xóa {dishesToDelete.Count} món ăn thành công!");
                    Console.ResetColor();
                }

                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
            }
        }


        private void BatchUpdatePrices()
        {
            Console.Write("Nhập phần trăm thay đổi giá (+ để tăng, - để giảm): ");
            decimal percent = decimal.Parse(Console.ReadLine());

            Console.Write("Áp dụng cho nhóm món (để trống nếu áp dụng cho tất cả): ");
            string categoryFilter = Console.ReadLine();

            var dishesToUpdate = dishes.Values.Where(d =>
                string.IsNullOrEmpty(categoryFilter) || d.Category == categoryFilter).ToList();

            Console.WriteLine($"Sẽ cập nhật {dishesToUpdate.Count} món ăn");
            Console.Write("Xác nhận (y/n): ");
            if (Console.ReadLine().ToLower() == "y")
            {
                foreach (var dish in dishesToUpdate)
                {
                    dish.Price = dish.Price * (1 + percent / 100);
                }

                auditLogs.Add(new AuditLog(currentUser.Username, "BATCH_UPDATE_PRICES", "DISH", "",
                    $"Cập nhật {dishesToUpdate.Count} món, thay đổi {percent}%"));
                SaveAllData();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Cập nhật giá hàng loạt thành công!");
                Console.ResetColor();
            }
        }

        private void BatchUpdateAvailability()
        {
            Console.Write("Đặt trạng thái sẵn có (1-Có sẵn, 0-Hết hàng): ");
            bool isAvailable = Console.ReadLine() == "1";

            Console.Write("Áp dụng cho nhóm món (để trống nếu áp dụng cho tất cả): ");
            string categoryFilter = Console.ReadLine();

            var dishesToUpdate = dishes.Values.Where(d =>
                string.IsNullOrEmpty(categoryFilter) || d.Category == categoryFilter).ToList();

            Console.WriteLine($"Sẽ cập nhật {dishesToUpdate.Count} món ăn");
            Console.Write("Xác nhận (y/n): ");
            if (Console.ReadLine().ToLower() == "y")
            {
                foreach (var dish in dishesToUpdate)
                {
                    dish.IsAvailable = isAvailable;
                }

                auditLogs.Add(new AuditLog(currentUser.Username, "BATCH_UPDATE_AVAILABILITY", "DISH", "",
                    $"Cập nhật {dishesToUpdate.Count} món, trạng thái: {(isAvailable ? "Có sẵn" : "Hết hàng")}"));
                SaveAllData();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Cập nhật trạng thái hàng loạt thành công!");
                Console.ResetColor();
            }
        }

        private void DisplayDishesSimple(int page = 1, int pageSize = 20)
        {
            Console.Clear();
            var dishList = dishes.Values.ToList();
            int totalPages = (int)Math.Ceiling(dishList.Count / (double)pageSize);

            if (dishList.Count == 0)
            {
                Console.WriteLine("Chưa có món ăn nào trong hệ thống!");
                return;
            }

            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                               DANH SÁCH MÓN ĂN                               ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-8} {1,-25} {2,-15} {3,-12} {4,-10} ║", "Mã", "Tên món", "Nhóm", "Giá", "Tình trạng");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

            var pagedDishes = dishList.Skip((page - 1) * pageSize).Take(pageSize);

            foreach (var dish in pagedDishes)
            {
                string status = dish.IsAvailable ? "Có sẵn" : "Hết hàng";
                Console.WriteLine("║ {0,-8} {1,-25} {2,-15} {3,-12} {4,-10} ║",
                    dish.Id,
                    TruncateString(dish.Name, 25),
                    TruncateString(dish.Category, 15),
                    $"{dish.Price:N0}đ",
                    status);
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine($"\nTrang {page}/{totalPages} - Tổng cộng: {dishList.Count} món");

            if (totalPages > 1)
            {
                if (page > 1) Console.Write("[P] Trang trước | ");
                if (page < totalPages) Console.Write("[N] Trang sau | ");
                Console.WriteLine("[0] Thoát");
                Console.Write("Chọn: ");

                string choice = Console.ReadLine().ToLower();
                if (choice == "n" && page < totalPages)
                    DisplayDishesSimple(page + 1, pageSize);
                else if (choice == "p" && page > 1)
                    DisplayDishesSimple(page - 1, pageSize);
            }
            else
            {
                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
            }
        }

        private void DisplayIngredientsForSelection(int page = 1, int pageSize = 20)
        {
            Console.Clear();
            var ingredientList = ingredients.Values.ToList();
            int totalPages = (int)Math.Ceiling(ingredientList.Count / (double)pageSize);

            if (ingredientList.Count == 0)
            {
                Console.WriteLine("Chưa có nguyên liệu nào!");
                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                               DANH SÁCH NGUYÊN LIỆU                          ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-8} {1,-25} {2,-10} {3,-8} {4,-10} {5,-12} ║",
                "Mã", "Tên", "Đơn vị", "Số lượng", "Tối thiểu", "Giá/ĐV");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

            var pagedIngredients = ingredientList.Skip((page - 1) * pageSize).Take(pageSize);

            foreach (var ing in pagedIngredients)
            {
                string warning = ing.IsLowStock ? "⚠️" : "";
                Console.WriteLine("║ {0,-8} {1,-25} {2,-10} {3,-8} {4,-10} {5,-12} {6,-2} ║",
                    ing.Id,
                    TruncateString(ing.Name, 25),
                    ing.Unit,
                    ing.Quantity,
                    ing.MinQuantity,
                    $"{ing.PricePerUnit:N0}đ",
                    warning);
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine($"\nTrang {page}/{totalPages} - Tổng cộng: {ingredientList.Count} nguyên liệu");

            if (totalPages > 1)
            {
                if (page > 1) Console.Write("[P] Trang trước | ");
                if (page < totalPages) Console.Write("[N] Trang sau | ");
                Console.WriteLine("[0] Thoát");
                Console.Write("Chọn: ");

                string choice = Console.ReadLine().ToLower();
                if (choice == "n" && page < totalPages)
                    DisplayIngredientsForSelection(page + 1, pageSize);
                else if (choice == "p" && page > 1)
                    DisplayIngredientsForSelection(page - 1, pageSize);
            }
            else
            {
                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
            }
        }
        // ==================== DATA PERSISTENCE ====================
        private void EnsureDataDirectory()
        {
            if (!Directory.Exists(DATA_FOLDER))
            {
                Directory.CreateDirectory(DATA_FOLDER);
            }
        }

        private void EnsureDownloadDirectory()
        {
            string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
            if (!Directory.Exists(downloadPath))
            {
                Directory.CreateDirectory(downloadPath);
            }
        }

        private void LoadAllData()
        {
            users = LoadData<Dictionary<string, User>>("users.json") ?? new Dictionary<string, User>();
            ingredients = LoadData<Dictionary<string, Ingredient>>("ingredients.json") ?? new Dictionary<string, Ingredient>();
            dishes = LoadData<Dictionary<string, Dish>>("dishes.json") ?? new Dictionary<string, Dish>();
            combos = LoadData<Dictionary<string, Combo>>("combos.json") ?? new Dictionary<string, Combo>();
            orders = LoadData<Dictionary<string, Order>>("orders.json") ?? new Dictionary<string, Order>();
            auditLogs = LoadData<List<AuditLog>>("audit_logs.json") ?? new List<AuditLog>();

            // Tạo dữ liệu mẫu nếu chưa có
            if (users.Count == 0) CreateSampleUsers();
            if (ingredients.Count == 0) CreateSampleIngredients();
            if (dishes.Count == 0) CreateSampleDishes();
        }

        private T LoadData<T>(string fileName)
        {
            try
            {
                string filePath = Path.Combine(DATA_FOLDER, fileName);
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi đọc file {fileName}: {ex.Message}");
            }
            return default(T);
        }

        public void SaveAllData()
        {
            SaveData("users.json", users);
            SaveData("ingredients.json", ingredients);
            SaveData("dishes.json", dishes);
            SaveData("combos.json", combos);
            SaveData("orders.json", orders);
            SaveData("audit_logs.json", auditLogs);
        }

        private void SaveData<T>(string fileName, T data)
        {
            try
            {
                string filePath = Path.Combine(DATA_FOLDER, fileName);
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi ghi file {fileName}: {ex.Message}");
            }
        }

        private void CreateSampleUsers()
        {
            users["admin"] = new User("admin", SecurityService.HashPassword("admin123"), UserRole.Admin, "Quản trị viên");
            users["manager"] = new User("manager", SecurityService.HashPassword("manager123"), UserRole.Manager, "Quản lý nhà hàng");
            users["staff"] = new User("staff", SecurityService.HashPassword("staff123"), UserRole.Staff, "Nhân viên phục vụ");
        }

        private void CreateSampleIngredients()
        {
            // Tạo 50 nguyên liệu mẫu
            var ingredientNames = new[]
            {
                "Thịt bò", "Thịt heo", "Thịt gà", "Tôm", "Cá hồi", "Mực", "Cá thu", "Cá chép", "Cá trắm", "Cá diêu hồng",
                "Rau xà lách", "Cà chua", "Hành tây", "Tỏi", "Gừng", "Hành lá", "Rau mùi", "Rau răm", "Rau thơm", "Rau muống",
                "Cải thảo", "Cải ngọt", "Cải xanh", "Bắp cải", "Súp lơ", "Cà rốt", "Khoai tây", "Khoai lang", "Bí đỏ", "Bí xanh",
                "Măng tây", "Nấm hương", "Nấm rơm", "Nấm kim châm", "Nấm đùi gà", "Đậu phụ", "Đậu que", "Đậu Hà Lan", "Đậu đỏ", "Đậu xanh",
                "Gạo", "Mì Ý", "Bún", "Miến", "Phở", "Bánh mì", "Bánh phở", "Bánh đa", "Bánh tráng", "Bánh cuốn"
            };

            var units = new[] { "kg", "g", "lít", "ml", "quả", "bó", "củ", "nhánh", "gói", "túi" };
            var random = new Random();

            for (int i = 1; i <= 50; i++)
            {
                string id = "ING" + i.ToString("D3");
                string name = ingredientNames[random.Next(ingredientNames.Length)] + " " + (i % 10 + 1);
                string unit = units[random.Next(units.Length)];
                decimal quantity = random.Next(1, 100);
                decimal minQuantity = quantity * 0.2m;
                decimal price = random.Next(1000, 50000);

                var ingredient = new Ingredient(id, name, unit, quantity, minQuantity, price);
                ingredients[id] = ingredient;
            }
        }

        private void CreateSampleDishes()
        {
            // Tạo 100 món ăn mẫu
            var dishNames = new[]
            {
                "Phở", "Bún", "Cơm", "Mì", "Bánh", "Gỏi", "Nem", "Chả", "Lẩu", "Nướng",
                "Xào", "Hấp", "Chiên", "Rán", "Kho", "Luộc", "Nấu", "Om", "Rang", "Sốt"
            };

            var dishTypes = new[]
            {
                "bò", "gà", "heo", "tôm", "cá", "mực", "cua", "ghẹ", "ốc", "hến",
                "thập cẩm", "chay", "hải sản", "đặc biệt", "truyền thống", "hiện đại"
            };

            var random = new Random();

            for (int i = 1; i <= 100; i++)
            {
                string id = "DISH" + i.ToString("D3");
                string name = dishNames[random.Next(dishNames.Length)] + " " + dishTypes[random.Next(dishTypes.Length)] + " " + (i % 20 + 1);
                string description = "Món ăn ngon " + name;
                decimal price = random.Next(20000, 300000);
                string category = dishCategories[random.Next(dishCategories.Count)];

                var dish = new Dish(id, name, description, price, category);

                // Thêm ngẫu nhiên 2-5 nguyên liệu cho mỗi món
                int ingredientCount = random.Next(2, 6);
                var availableIngredients = ingredients.Values.ToList();

                for (int j = 0; j < ingredientCount; j++)
                {
                    var ingredient = availableIngredients[random.Next(availableIngredients.Count)];
                    decimal quantity = (decimal)(random.NextDouble() * 0.5 + 0.1); // 0.1 - 0.6
                    dish.Ingredients[ingredient.Id] = quantity;
                }

                dishes[id] = dish;
            }
        }

        // ==================== UTILITY METHODS ====================
        private void ShowAccessDenied()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Bạn không có quyền truy cập chức năng này!");
            Console.ResetColor();
            Thread.Sleep(2000);
        }

        private string TruncateString(string str, int maxLength)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return str.Length <= maxLength ? str : str.Substring(0, maxLength - 3) + "...";
        }

        // ==================== DISH MANAGEMENT METHODS ====================
       

        private void SearchDishes()
        {
            Console.Clear();
            Console.WriteLine("TÌM KIẾM MÓN ĂN");
            Console.WriteLine("================");

            Console.Write("Nhập từ khóa tìm kiếm: ");
            string keyword = Console.ReadLine().ToLower();

            var results = dishes.Values.Where(d =>
                d.Name.ToLower().Contains(keyword) ||
                d.Description.ToLower().Contains(keyword) ||
                d.Category.ToLower().Contains(keyword) ||
                d.Id.ToLower().Contains(keyword)).ToList();

            Console.WriteLine($"\nTìm thấy {results.Count} kết quả:");

            if (results.Any())
            {
                Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                       KẾT QUẢ TÌM KIẾM                        ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
                Console.WriteLine("║ {0,-8} {1,-25} {2,-15} {3,-12} ║", "Mã", "Tên món", "Nhóm", "Giá");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

                foreach (var dish in results.Take(20))
                {
                    Console.WriteLine("║ {0,-8} {1,-25} {2,-15} {3,-12} ║",
                        dish.Id,
                        TruncateString(dish.Name, 25),
                        TruncateString(dish.Category, 15),
                        $"{dish.Price:N0}đ");
                }
                Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void FilterDishes()
        {
            Console.Clear();
            Console.WriteLine("LỌC MÓN ĂN");
            Console.WriteLine("===========");

            Console.WriteLine("1. Theo giá (thấp -> cao)");
            Console.WriteLine("2. Theo giá (cao -> thấp)");
            Console.WriteLine("3. Theo nhóm món");
            Console.WriteLine("4. Món còn nguyên liệu");
            Console.WriteLine("5. Món bán chạy nhất");
            Console.Write("Chọn tiêu chí lọc: ");

            string choice = Console.ReadLine();
            IEnumerable<Dish> filteredDishes = dishes.Values;

            switch (choice)
            {
                case "1":
                    filteredDishes = filteredDishes.OrderBy(d => d.Price);
                    break;
                case "2":
                    filteredDishes = filteredDishes.OrderByDescending(d => d.Price);
                    break;
                case "3":
                    Console.Write("Nhập nhóm món: ");
                    string category = Console.ReadLine();
                    filteredDishes = filteredDishes.Where(d => d.Category.ToLower().Contains(category.ToLower()));
                    break;
                case "4":
                    filteredDishes = filteredDishes.Where(d => CheckDishIngredients(d));
                    break;
                case "5":
                    filteredDishes = filteredDishes.OrderByDescending(d => d.SalesCount);
                    break;
                default:
                    Console.WriteLine("Lựa chọn không hợp lệ!");
                    return;
            }

            var results = filteredDishes.ToList();
            Console.WriteLine($"\nTìm thấy {results.Count} kết quả:");

            if (results.Any())
            {
                Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                               KẾT QUẢ LỌC                                   ║");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
                Console.WriteLine("║ {0,-8} {1,-25} {2,-15} {3,-12} {4,-10} ║", "Mã", "Tên món", "Nhóm", "Giá", "Đã bán");
                Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

                foreach (var dish in results.Take(20))
                {
                    Console.WriteLine("║ {0,-8} {1,-25} {2,-15} {3,-12} {4,-10} ║",
                        dish.Id,
                        TruncateString(dish.Name, 25),
                        TruncateString(dish.Category, 15),
                        $"{dish.Price:N0}đ",
                        dish.SalesCount);
                }
                Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ShowDishDetail()
        {
            Console.Clear();
            Console.WriteLine("CHI TIẾT MÓN ĂN");
            Console.WriteLine("================");

            // Hiển thị danh sách món ăn để chọn
            DisplayDishesSimple();

            Console.Write("\nNhập mã món ăn: ");
            string id = Console.ReadLine();

            if (!dishes.ContainsKey(id))
            {
                Console.WriteLine("Món ăn không tồn tại!");
                Console.ReadKey();
                return;
            }

            var dish = dishes[id];

            Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                       CHI TIẾT MÓN ĂN                        ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Mã món:", dish.Id);
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Tên món:", dish.Name);
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Mô tả:", TruncateString(dish.Description, 30));
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Giá:", $"{dish.Price:N0}đ");
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Nhóm:", dish.Category);
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Tình trạng:", dish.IsAvailable ? "✅ Có sẵn" : "❌ Hết hàng");
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Số lượt bán:", dish.SalesCount);

            // Tính chi phí nguyên liệu
            decimal cost = dish.CalculateCost(ingredients);
            decimal profit = dish.Price - cost;
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Chi phí NL:", $"{cost:N0}đ");
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Lợi nhuận:", $"{profit:N0}đ");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

            Console.WriteLine("║ {0,-40} ║", "NGUYÊN LIỆU:");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

            if (dish.Ingredients.Any())
            {
                foreach (var ing in dish.Ingredients)
                {
                    if (ingredients.ContainsKey(ing.Key))
                    {
                        var ingredient = ingredients[ing.Key];
                        string status = ingredient.Quantity >= ing.Value ? "✅" : "❌";
                        Console.WriteLine("║ {0,-2} {1,-20} {2,-8} {3,-10} ║",
                            status,
                            TruncateString(ingredient.Name, 20),
                            $"{ing.Value} {ingredient.Unit}",
                            $"{ingredient.Quantity} tồn");
                    }
                }
            }
            else
            {
                Console.WriteLine("║ {0,-40} ║", "Chưa có nguyên liệu");
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        // ==================== COMBO MANAGEMENT ====================
        private void ShowComboManagementMenu()
        {
            while (true)
            {
                Console.Clear();
                DisplayHeader();
                Console.WriteLine("╔═════════════════════════════════════════════╗");
                Console.WriteLine("║         QUẢN LÝ COMBO & KHUYẾN MÃI          ║");
                Console.WriteLine("╠═════════════════════════════════════════════╣");
                Console.WriteLine("║ 1. Xem danh sách combo                      ║");
                Console.WriteLine("║ 2. Tạo combo mới                            ║");
                Console.WriteLine("║ 3. Cập nhật combo                           ║");
                Console.WriteLine("║ 4. Xóa combo                                ║");
                Console.WriteLine("║ 5. Tự động sinh combo                       ║");
                Console.WriteLine("║ 6. Thống kê combo bán chạy                  ║");
                Console.WriteLine("║ 7. Xem chi tiết combo                       ║");
                Console.WriteLine("║ 8. Liệt kê combo thực đơn tiệc              ║");
                Console.WriteLine("║ 0. Quay lại                                 ║");
                Console.WriteLine("╚═════════════════════════════════════════════╝");
                Console.Write("Chọn chức năng (X để thoát): ");

                string choice = Console.ReadLine();
                if (choice.ToLower() == "x") return;

                switch (choice)
                {
                    case "1": DisplayCombos(); break;
                    case "2": CreateCombo(); break;
                    case "3": UpdateCombo(); break;
                    case "4": DeleteCombo(); break;
                    case "5": AutoGenerateCombo(); break;
                    case "6": ShowComboSalesReport(); break;
                    case "7": ShowComboDetail(); break;
                    case "8": GeneratePartyMenuCombos(); break;
                    case "0": return;
                    default: Console.WriteLine("Lựa chọn không hợp lệ!"); Thread.Sleep(1000); break;
                }
            }
        }

        private void GeneratePartyMenuCombos()
        {
            Console.Clear();
            Console.WriteLine("LIỆT KÊ COMBO THỰC ĐƠN TIỆC");
            Console.WriteLine("=============================");
            Console.WriteLine("(Nhập X để hủy bất kỳ lúc nào)");

            try
            {
                Console.WriteLine("Chọn loại tiệc:");
                Console.WriteLine("1. Tiệc cưới");
                Console.WriteLine("2. Tiệc sinh nhật");
                Console.WriteLine("3. Tiệc công ty");
                Console.WriteLine("4. Tiệc gia đình");
                Console.WriteLine("5. Tự chọn");
                Console.Write("Chọn: ");

                string choice = Console.ReadLine();
                if (choice.ToLower() == "x") return;

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
                    case "5":
                        partyType = "Tự chọn";
                        suggestedCombos = GenerateCustomCombos();
                        break;
                    default:
                        Console.WriteLine("Lựa chọn không hợp lệ!");
                        return;
                }

                if (suggestedCombos.Any())
                {
                    Console.WriteLine($"\n🎉 COMBO GỢI Ý CHO {partyType.ToUpper()}:");
                    Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
                    Console.WriteLine("║                           DANH SÁCH COMBO GỢI Ý                                ║");
                    Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
                    Console.WriteLine("║ {0,-8} {1,-25} {2,-8} {3,-12} {4,-12} ║",
                        "Mã", "Tên combo", "Số món", "Giá gốc", "Giá KM");
                    Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

                    foreach (var combo in suggestedCombos)
                    {
                        combo.CalculateOriginalPrice(dishes);
                        Console.WriteLine("║ {0,-8} {1,-25} {2,-8} {3,-12} {4,-12} ║",
                            combo.Id,
                            TruncateString(combo.Name, 25),
                            combo.DishIds.Count,
                            $"{combo.OriginalPrice:N0}đ",
                            $"{combo.FinalPrice:N0}đ");
                    }
                    Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");

                    // Xuất file gợi ý combo
                    ExportPartyMenuCombos(suggestedCombos, partyType);
                }
                else
                {
                    Console.WriteLine("Không có combo nào phù hợp!");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private List<Combo> GenerateWeddingCombos()
        {
            var weddingCombos = new List<Combo>();
            var availableDishes = dishes.Values.Where(d => d.IsAvailable).ToList();

            // Combo cao cấp
            var premiumDishes = availableDishes.Where(d => d.Price > 100000 && d.Category != "Đồ uống").Take(6).ToList();
            if (premiumDishes.Count >= 4)
            {
                var premiumCombo = new Combo("WEDDING_PREMIUM", "Combo Cưới Cao Cấp", "Combo cao cấp cho tiệc cưới", 15);
                premiumCombo.DishIds.AddRange(premiumDishes.Take(4).Select(d => d.Id));
                weddingCombos.Add(premiumCombo);
            }

            // Combo tiêu chuẩn
            var standardDishes = availableDishes.Where(d => d.Price >= 50000 && d.Price <= 100000).Take(5).ToList();
            if (standardDishes.Count >= 3)
            {
                var standardCombo = new Combo("WEDDING_STANDARD", "Combo Cưới Tiêu Chuẩn", "Combo tiêu chuẩn cho tiệc cưới", 10);
                standardCombo.DishIds.AddRange(standardDishes.Take(3).Select(d => d.Id));
                weddingCombos.Add(standardCombo);
            }

            return weddingCombos;
        }

        private List<Combo> GenerateBirthdayCombos()
        {
            var birthdayCombos = new List<Combo>();
            var availableDishes = dishes.Values.Where(d => d.IsAvailable).ToList();

            // Combo gia đình
            var familyDishes = availableDishes.Where(d => d.Category == "Món chính" || d.Category == "Món phụ").Take(4).ToList();
            if (familyDishes.Count >= 3)
            {
                var familyCombo = new Combo("BIRTHDAY_FAMILY", "Combo Sinh Nhật Gia Đình", "Combo ấm cúng cho gia đình", 12);
                familyCombo.DishIds.AddRange(familyDishes.Take(3).Select(d => d.Id));
                birthdayCombos.Add(familyCombo);
            }

            // Combo bạn bè
            var friendDishes = availableDishes.Where(d => d.Category == "Món khai vị" || d.Category == "Món chính").Take(5).ToList();
            if (friendDishes.Count >= 4)
            {
                var friendCombo = new Combo("BIRTHDAY_FRIENDS", "Combo Sinh Nhật Bạn Bè", "Combo vui vẻ cho bạn bè", 15);
                friendCombo.DishIds.AddRange(friendDishes.Take(4).Select(d => d.Id));
                birthdayCombos.Add(friendCombo);
            }

            return birthdayCombos;
        }

        private List<Combo> GenerateCorporateCombos()
        {
            var corporateCombos = new List<Combo>();
            var availableDishes = dishes.Values.Where(d => d.IsAvailable).ToList();

            // Combo hội nghị
            var conferenceDishes = availableDishes.Where(d => d.Category == "Món khai vị" || d.Category == "Đồ uống").Take(6).ToList();
            if (conferenceDishes.Count >= 4)
            {
                var conferenceCombo = new Combo("CORP_CONFERENCE", "Combo Hội Nghị", "Combo chuyên nghiệp cho hội nghị", 8);
                conferenceCombo.DishIds.AddRange(conferenceDishes.Take(4).Select(d => d.Id));
                corporateCombos.Add(conferenceCombo);
            }

            // Combo tiệc công ty
            var partyDishes = availableDishes.Where(d => d.Price >= 30000 && d.Price <= 80000).Take(8).ToList();
            if (partyDishes.Count >= 5)
            {
                var partyCombo = new Combo("CORP_PARTY", "Combo Tiệc Công Ty", "Combo đa dạng cho tiệc công ty", 10);
                partyCombo.DishIds.AddRange(partyDishes.Take(5).Select(d => d.Id));
                corporateCombos.Add(partyCombo);
            }

            return corporateCombos;
        }

        private List<Combo> GenerateFamilyCombos()
        {
            var familyCombos = new List<Combo>();
            var availableDishes = dishes.Values.Where(d => d.IsAvailable).ToList();

            // Combo ấm cúng
            var cozyDishes = availableDishes.Where(d => d.Category == "Món chính" || d.Category == "Món phụ").Take(4).ToList();
            if (cozyDishes.Count >= 3)
            {
                var cozyCombo = new Combo("FAMILY_COZY", "Combo Gia Đình Ấm Cúng", "Combo ấm cúng cho bữa cơm gia đình", 5);
                cozyCombo.DishIds.AddRange(cozyDishes.Take(3).Select(d => d.Id));
                familyCombos.Add(cozyCombo);
            }

            // Combo đầy đủ
            var fullDishes = availableDishes.Where(d => d.Price <= 50000).Take(6).ToList();
            if (fullDishes.Count >= 4)
            {
                var fullCombo = new Combo("FAMILY_FULL", "Combo Gia Đình Đầy Đủ", "Combo đầy đủ dinh dưỡng", 8);
                fullCombo.DishIds.AddRange(fullDishes.Take(4).Select(d => d.Id));
                familyCombos.Add(fullCombo);
            }

            return familyCombos;
        }

        private List<Combo> GenerateCustomCombos()
        {
            var customCombos = new List<Combo>();
            var availableDishes = dishes.Values.Where(d => d.IsAvailable).ToList();

            Console.Write("Nhập số lượng món trong combo: ");
            string dishCountInput = Console.ReadLine();
            if (dishCountInput.ToLower() == "x") return customCombos;

            if (int.TryParse(dishCountInput, out int dishCount) && dishCount > 0)
            {
                Console.Write("Nhóm món ưu tiên (để trống nếu không): ");
                string preferredCategory = Console.ReadLine();
                if (preferredCategory.ToLower() == "x") return customCombos;

                Console.Write("Mức giá tối đa cho mỗi món (để trống nếu không): ");
                string maxPriceInput = Console.ReadLine();
                if (maxPriceInput.ToLower() == "x") return customCombos;

                decimal? maxPrice = null;
                if (!string.IsNullOrEmpty(maxPriceInput))
                {
                    maxPrice = decimal.Parse(maxPriceInput);
                }

                var filteredDishes = availableDishes.Where(d =>
                    (string.IsNullOrEmpty(preferredCategory) || d.Category.Contains(preferredCategory)) &&
                    (!maxPrice.HasValue || d.Price <= maxPrice.Value)).ToList();

                if (filteredDishes.Count >= dishCount)
                {
                    var selectedDishes = filteredDishes.Take(dishCount).ToList();
                    var customCombo = new Combo($"CUSTOM_{DateTime.Now:HHmmss}", "Combo Tự Chọn", "Combo được tùy chỉnh theo yêu cầu", 10);
                    customCombo.DishIds.AddRange(selectedDishes.Select(d => d.Id));
                    customCombos.Add(customCombo);
                }
            }

            return customCombos;
        }

        private void ExportPartyMenuCombos(List<Combo> combos, string partyType)
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"ComboThucDon_{partyType.Replace(" ", "_")}_{DateTime.Now:yyyyMMddHHmmss}.txt";
                string filePath = Path.Combine(downloadPath, fileName);

                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine($"COMBO THỰC ĐƠN {partyType.ToUpper()}");
                    writer.WriteLine($"Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                    writer.WriteLine("==========================================");
                    writer.WriteLine();

                    foreach (var combo in combos)
                    {
                        combo.CalculateOriginalPrice(dishes);
                        writer.WriteLine($"{combo.Name}:");
                        writer.WriteLine($"  - Giá gốc: {combo.OriginalPrice:N0}đ");
                        writer.WriteLine($"  - Giá khuyến mãi: {combo.FinalPrice:N0}đ");
                        writer.WriteLine($"  - Giảm giá: {combo.DiscountPercent}%");
                        writer.WriteLine($"  - Số món: {combo.DishIds.Count}");
                        writer.WriteLine("  - Danh sách món:");

                        foreach (var dishId in combo.DishIds)
                        {
                            if (dishes.ContainsKey(dishId))
                            {
                                var dish = dishes[dishId];
                                writer.WriteLine($"    + {dish.Name} - {dish.Price:N0}đ");
                            }
                        }
                        writer.WriteLine();
                    }
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n📄 Đã xuất file gợi ý combo: {filePath}");
                Console.ResetColor();

                auditLogs.Add(new AuditLog(currentUser.Username, "EXPORT_PARTY_MENU", "COMBO", "", $"Xuất combo {partyType}"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi xuất file: {ex.Message}");
            }
        }

        private void DisplayCombos(int page = 1, int pageSize = 20)
        {
            Console.Clear();
            var comboList = combos.Values.Where(c => c.IsActive).ToList();
            int totalPages = (int)Math.Ceiling(comboList.Count / (double)pageSize);

            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                                  DANH SÁCH COMBO                             ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-8} {1,-25} {2,-8} {3,-12} {4,-12} {5,-8} ║",
                "Mã", "Tên combo", "Số món", "Giá gốc", "Giá KM", "Giảm giá");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

            var pagedCombos = comboList.Skip((page - 1) * pageSize).Take(pageSize);

            foreach (var combo in pagedCombos)
            {
                combo.CalculateOriginalPrice(dishes);
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

            if (page > 1) Console.Write("[P] Trang trước | ");
            if (page < totalPages) Console.Write("[N] Trang sau | ");
            Console.WriteLine("[0] Thoát");
            Console.Write("Chọn: ");

            string choice = Console.ReadLine().ToLower();
            if (choice == "n" && page < totalPages)
                DisplayCombos(page + 1, pageSize);
            else if (choice == "p" && page > 1)
                DisplayCombos(page - 1, pageSize);
        }

        private void CreateCombo()
        {
            Console.Clear();
            Console.WriteLine("TẠO COMBO MỚI");
            Console.WriteLine("==============");

            try
            {
                Console.Write("Mã combo: ");
                string id = Console.ReadLine();

                if (combos.ContainsKey(id))
                {
                    Console.WriteLine("Mã combo đã tồn tại!");
                    Console.ReadKey();
                    return;
                }

                Console.Write("Tên combo: ");
                string name = Console.ReadLine();

                Console.Write("Mô tả: ");
                string description = Console.ReadLine();

                Console.Write("Phần trăm giảm giá: ");
                decimal discount = decimal.Parse(Console.ReadLine());

                var combo = new Combo(id, name, description, discount);

                // Hiển thị danh sách món ăn để chọn
                Console.WriteLine("\nDANH SÁCH MÓN ĂN CÓ SẴN:");
                DisplayDishesForCombo();

                // Thêm món vào combo
                Console.WriteLine("\nThêm món vào combo (để trống mã món để kết thúc):");
                while (true)
                {
                    Console.Write("Mã món ăn: ");
                    string dishId = Console.ReadLine();
                    if (string.IsNullOrEmpty(dishId)) break;

                    if (!dishes.ContainsKey(dishId))
                    {
                        Console.WriteLine("Món ăn không tồn tại!");
                        continue;
                    }

                    if (combo.DishIds.Contains(dishId))
                    {
                        Console.WriteLine("Món ăn đã có trong combo!");
                        continue;
                    }

                    combo.DishIds.Add(dishId);
                    Console.WriteLine($"Đã thêm món: {dishes[dishId].Name}");
                }

                if (combo.DishIds.Count == 0)
                {
                    Console.WriteLine("Combo phải có ít nhất 1 món!");
                    Console.ReadKey();
                    return;
                }

                combo.CalculateOriginalPrice(dishes);
                combos[id] = combo;

                auditLogs.Add(new AuditLog(currentUser.Username, "CREATE_COMBO", "COMBO", id, $"Tạo combo: {name}"));
                SaveAllData();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\nTạo combo thành công!");
                Console.WriteLine($"- Tên combo: {combo.Name}");
                Console.WriteLine($"- Số món: {combo.DishIds.Count}");
                Console.WriteLine($"- Giá gốc: {combo.OriginalPrice:N0}đ");
                Console.WriteLine($"- Giá khuyến mãi: {combo.FinalPrice:N0}đ");
                Console.WriteLine($"- Tiết kiệm: {combo.OriginalPrice - combo.FinalPrice:N0}đ");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void DisplayDishesForCombo()
        {
            Console.WriteLine("─────────────────────────────────────────────────────────");
            int count = 0;
            foreach (var dish in dishes.Values.Where(d => d.IsAvailable).Take(30))
            {
                Console.WriteLine($"{dish.Id} - {dish.Name} - {dish.Price:N0}đ - {dish.Category}");
                count++;
                if (count % 10 == 0) Console.WriteLine();
            }
            if (dishes.Count > 30)
            {
                Console.WriteLine($"... và {dishes.Count - 30} món khác");
            }
        }

        private void UpdateCombo()
        {
            Console.Clear();
            Console.WriteLine("CẬP NHẬT COMBO");
            Console.WriteLine("===============");

            // Hiển thị danh sách combo để chọn
            DisplayCombosSimple();

            try
            {
                Console.Write("\nNhập mã combo cần cập nhật: ");
                string id = Console.ReadLine();

                if (!combos.ContainsKey(id))
                {
                    Console.WriteLine("Combo không tồn tại!");
                    Console.ReadKey();
                    return;
                }

                var combo = combos[id];

                Console.Write($"Tên combo ({combo.Name}): ");
                string name = Console.ReadLine();
                if (!string.IsNullOrEmpty(name)) combo.Name = name;

                Console.Write($"Mô tả ({combo.Description}): ");
                string description = Console.ReadLine();
                if (!string.IsNullOrEmpty(description)) combo.Description = description;

                Console.Write($"Phần trăm giảm giá ({combo.DiscountPercent}%): ");
                string discountStr = Console.ReadLine();
                if (!string.IsNullOrEmpty(discountStr)) combo.DiscountPercent = decimal.Parse(discountStr);

                Console.WriteLine("\nQuản lý món trong combo:");
                Console.WriteLine("1. Thêm món");
                Console.WriteLine("2. Xóa món");
                Console.WriteLine("3. Xem món hiện tại");
                Console.WriteLine("4. Giữ nguyên");
                Console.Write("Chọn: ");

                string choice = Console.ReadLine();
                if (choice == "1")
                {
                    Console.Write("Mã món ăn cần thêm: ");
                    string dishId = Console.ReadLine();
                    if (dishes.ContainsKey(dishId) && !combo.DishIds.Contains(dishId))
                    {
                        combo.DishIds.Add(dishId);
                        Console.WriteLine($"Đã thêm món: {dishes[dishId].Name}");
                    }
                    else
                    {
                        Console.WriteLine("Món ăn không tồn tại hoặc đã có trong combo!");
                    }
                }
                else if (choice == "2")
                {
                    Console.Write("Mã món ăn cần xóa: ");
                    string dishId = Console.ReadLine();
                    if (combo.DishIds.Contains(dishId))
                    {
                        combo.DishIds.Remove(dishId);
                        Console.WriteLine("Đã xóa món khỏi combo!");
                    }
                    else
                    {
                        Console.WriteLine("Món ăn không có trong combo!");
                    }
                }
                else if (choice == "3")
                {
                    Console.WriteLine("\nCác món trong combo:");
                    foreach (var dishId in combo.DishIds)
                    {
                        if (dishes.ContainsKey(dishId))
                        {
                            Console.WriteLine($"- {dishes[dishId].Name} ({dishes[dishId].Price:N0}đ)");
                        }
                    }
                }

                combo.CalculateOriginalPrice(dishes);
                auditLogs.Add(new AuditLog(currentUser.Username, "UPDATE_COMBO", "COMBO", id, $"Cập nhật combo: {combo.Name}"));
                SaveAllData();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Cập nhật combo thành công!");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void DeleteCombo()
        {
            Console.Clear();
            Console.WriteLine("XÓA COMBO");
            Console.WriteLine("==========");

            // Hiển thị danh sách combo để chọn
            DisplayCombosSimple();

            try
            {
                Console.Write("\nNhập mã combo cần xóa: ");
                string id = Console.ReadLine();

                if (!combos.ContainsKey(id))
                {
                    Console.WriteLine("Combo không tồn tại!");
                    Console.ReadKey();
                    return;
                }

                var combo = combos[id];

                Console.WriteLine($"\nThông tin combo:");
                Console.WriteLine($"- Tên: {combo.Name}");
                Console.WriteLine($"- Số món: {combo.DishIds.Count}");
                Console.WriteLine($"- Giá: {combo.FinalPrice:N0}đ");
                Console.WriteLine($"- Đã bán: {combo.SalesCount}");

                Console.WriteLine($"\nBạn có chắc chắn muốn xóa combo '{combo.Name}'? (y/n)");
                string confirm = Console.ReadLine();

                if (confirm.ToLower() == "y")
                {
                    // Chỉ đánh dấu là không active thay vì xóa hoàn toàn
                    combo.IsActive = false;
                    auditLogs.Add(new AuditLog(currentUser.Username, "DELETE_COMBO", "COMBO", id, $"Xóa combo: {combo.Name}"));
                    SaveAllData();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Xóa combo thành công!");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void DisplayCombosSimple()
        {
            Console.WriteLine("\nDANH SÁCH COMBO:");
            Console.WriteLine("─────────────────────────────────────────────────────────");
            foreach (var combo in combos.Values.Where(c => c.IsActive).Take(20))
            {
                combo.CalculateOriginalPrice(dishes);
                Console.WriteLine($"{combo.Id} - {combo.Name} - {combo.FinalPrice:N0}đ - {combo.DishIds.Count} món");
            }
            if (combos.Count > 20)
            {
                Console.WriteLine($"... và {combos.Count - 20} combo khác");
            }
        }

        private void AutoGenerateCombo()
        {
            Console.Clear();
            Console.WriteLine("TỰ ĐỘNG SINH COMBO");
            Console.WriteLine("===================");

            Console.WriteLine("1. Combo theo nhóm món");
            Console.WriteLine("2. Combo khuyến mãi theo nguyên liệu");
            Console.WriteLine("3. Combo ngẫu nhiên");
            Console.Write("Chọn loại combo: ");

            string choice = Console.ReadLine();
            if (choice == "1")
            {
                GenerateCategoryCombo();
            }
            else if (choice == "2")
            {
                GeneratePromotionCombo();
            }
            else if (choice == "3")
            {
                GenerateRandomCombo();
            }
        }

        private void GenerateCategoryCombo()
        {
            Console.Write("Nhập nhóm món: ");
            string category = Console.ReadLine();

            var categoryDishes = dishes.Values.Where(d =>
                d.Category.ToLower().Contains(category.ToLower()) && d.IsAvailable).Take(4).ToList();

            if (categoryDishes.Count < 2)
            {
                Console.WriteLine("Không đủ món để tạo combo!");
                Console.ReadKey();
                return;
            }

            string comboId = "COMBO_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            var combo = new Combo(comboId, $"Combo {category}", $"Combo tự động sinh cho nhóm {category}", 15);

            foreach (var dish in categoryDishes)
            {
                combo.DishIds.Add(dish.Id);
            }

            combo.CalculateOriginalPrice(dishes);
            combos[comboId] = combo;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Đã tạo combo {comboId} với {combo.DishIds.Count} món!");
            Console.WriteLine($"- Giá gốc: {combo.OriginalPrice:N0}đ");
            Console.WriteLine($"- Giá KM: {combo.FinalPrice:N0}đ");
            Console.ResetColor();

            auditLogs.Add(new AuditLog(currentUser.Username, "AUTO_GENERATE_COMBO", "COMBO", comboId, $"Combo nhóm {category}"));
            SaveAllData();

            Console.ReadKey();
        }

        private void GeneratePromotionCombo()
        {
            // Tìm các món có nguyên liệu sắp hết để khuyến mãi
            var promotionDishes = dishes.Values.Where(d =>
                d.IsAvailable && d.Ingredients.Any(ing =>
                    ingredients.ContainsKey(ing.Key) && ingredients[ing.Key].IsLowStock)).Take(3).ToList();

            if (promotionDishes.Count < 2)
            {
                Console.WriteLine("Không đủ món để tạo combo khuyến mãi!");
                Console.ReadKey();
                return;
            }

            string comboId = "PROMO_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            var combo = new Combo(comboId, "Combo Khuyến Mãi", "Combo khuyến mãi nguyên liệu sắp hết", 20);

            foreach (var dish in promotionDishes)
            {
                combo.DishIds.Add(dish.Id);
            }

            combo.CalculateOriginalPrice(dishes);
            combos[comboId] = combo;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Đã tạo combo khuyến mãi {comboId} với {combo.DishIds.Count} món!");
            Console.WriteLine($"- Giá gốc: {combo.OriginalPrice:N0}đ");
            Console.WriteLine($"- Giá KM: {combo.FinalPrice:N0}đ");
            Console.ResetColor();

            auditLogs.Add(new AuditLog(currentUser.Username, "AUTO_GENERATE_PROMO_COMBO", "COMBO", comboId, "Combo khuyến mãi"));
            SaveAllData();

            Console.ReadKey();
        }

        private void GenerateRandomCombo()
        {
            var availableDishes = dishes.Values.Where(d => d.IsAvailable).ToList();
            if (availableDishes.Count < 3)
            {
                Console.WriteLine("Không đủ món để tạo combo!");
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

            combo.CalculateOriginalPrice(dishes);
            combos[comboId] = combo;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Đã tạo combo ngẫu nhiên {comboId} với {combo.DishIds.Count} món!");
            Console.WriteLine($"- Giá gốc: {combo.OriginalPrice:N0}đ");
            Console.WriteLine($"- Giá KM: {combo.FinalPrice:N0}đ");
            Console.ResetColor();

            auditLogs.Add(new AuditLog(currentUser.Username, "AUTO_GENERATE_RANDOM_COMBO", "COMBO", comboId, "Combo ngẫu nhiên"));
            SaveAllData();

            Console.ReadKey();
        }

        private void ShowComboSalesReport()
        {
            Console.Clear();
            Console.WriteLine("THỐNG KÊ COMBO BÁN CHẠY");
            Console.WriteLine("=========================");

            var topCombos = combos.Values
                .Where(c => c.IsActive && c.SalesCount > 0)
                .OrderByDescending(c => c.SalesCount)
                .Take(10)
                .ToList();

            if (!topCombos.Any())
            {
                Console.WriteLine("Chưa có combo nào được bán!");
                Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                           TOP COMBO BÁN CHẠY                                 ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-3} {1,-25} {2,-10} {3,-15} {4,-15} ║",
                "STT", "Tên combo", "Số lượt", "Doanh thu", "Giá trung bình");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

            for (int i = 0; i < topCombos.Count; i++)
            {
                var combo = topCombos[i];
                combo.CalculateOriginalPrice(dishes);
                decimal revenue = combo.FinalPrice * combo.SalesCount;
                decimal avgPrice = combo.FinalPrice;

                Console.WriteLine("║ {0,-3} {1,-25} {2,-10} {3,-15} {4,-15} ║",
                    i + 1,
                    TruncateString(combo.Name, 25),
                    combo.SalesCount,
                    $"{revenue:N0}đ",
                    $"{avgPrice:N0}đ");
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");

            // Hiển thị biểu đồ ASCII đơn giản
            Console.WriteLine("\n📊 BIỂU ĐỒ DOANH SỐ COMBO:");
            int maxSales = topCombos.Max(c => c.SalesCount);

            foreach (var combo in topCombos.Take(5))
            {
                int barLength = maxSales > 0 ? (int)((double)combo.SalesCount / maxSales * 50) : 0;
                string bar = new string('█', barLength);
                Console.WriteLine($"{TruncateString(combo.Name, 20)}: {bar} {combo.SalesCount} lượt");
            }

            // Xuất file báo cáo
            ExportComboReport(topCombos);

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ShowComboDetail()
        {
            Console.Clear();
            Console.WriteLine("CHI TIẾT COMBO");
            Console.WriteLine("===============");

            // Hiển thị danh sách combo để chọn
            DisplayCombosSimple();

            Console.Write("\nNhập mã combo: ");
            string id = Console.ReadLine();

            if (!combos.ContainsKey(id) || !combos[id].IsActive)
            {
                Console.WriteLine("Combo không tồn tại!");
                Console.ReadKey();
                return;
            }

            var combo = combos[id];
            combo.CalculateOriginalPrice(dishes);

            Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                       CHI TIẾT COMBO                         ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Mã combo:", combo.Id);
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Tên combo:", combo.Name);
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Mô tả:", TruncateString(combo.Description, 30));
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Giảm giá:", $"{combo.DiscountPercent}%");
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Giá gốc:", $"{combo.OriginalPrice:N0}đ");
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Giá KM:", $"{combo.FinalPrice:N0}đ");
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Tiết kiệm:", $"{combo.OriginalPrice - combo.FinalPrice:N0}đ");
            Console.WriteLine("║ {0,-15} {1,-30} ║", "Số lượt bán:", combo.SalesCount);
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

            Console.WriteLine("║ {0,-40} ║", "DANH SÁCH MÓN TRONG COMBO:");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

            if (combo.DishIds.Any())
            {
                foreach (var dishId in combo.DishIds)
                {
                    if (dishes.ContainsKey(dishId))
                    {
                        var dish = dishes[dishId];
                        string status = dish.IsAvailable ? "✅" : "❌";
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

        // ==================== ORDER MANAGEMENT ====================
        private void ShowOrderManagementMenu()
        {
            while (true)
            {
                Console.Clear();
                DisplayHeader();
                Console.WriteLine("╔═════════════════════════════════════════════╗");
                Console.WriteLine("║          BÁN HÀNG / ĐƠN ĐẶT MÓN             ║");
                Console.WriteLine("╠═════════════════════════════════════════════╣");
                Console.WriteLine("║ 1. Tạo đơn hàng mới                         ║");
                Console.WriteLine("║ 2. Xem danh sách đơn hàng                   ║");
                Console.WriteLine("║ 3. Cập nhật trạng thái đơn hàng             ║");
                Console.WriteLine("║ 4. Xem chi tiết đơn hàng                    ║");
                Console.WriteLine("║ 5. Thống kê đơn hàng                        ║");
                Console.WriteLine("║ 6. Xuất danh sách đơn hàng                  ║");
                Console.WriteLine("║ 0. Quay lại                                 ║");
                Console.WriteLine("╚═════════════════════════════════════════════╝");
                Console.Write("Chọn chức năng: ");

                string choice = Console.ReadLine();
                switch (choice)
                {
                    case "1": CreateOrder(); break;
                    case "2": DisplayOrders(); break;
                    case "3": UpdateOrderStatus(); break;
                    case "4": ShowOrderDetail(); break;
                    case "5": ShowOrderStatistics(); break;
                    case "6": ExportOrders(); break;
                    case "0": return;
                    default: Console.WriteLine("Lựa chọn không hợp lệ!"); Thread.Sleep(1000); break;
                }
            }
        }

        private void CreateOrder()
        {
            Console.Clear();
            Console.WriteLine("TẠO ĐƠN HÀNG MỚI");
            Console.WriteLine("=================");

            try
            {
                string orderId = "ORD_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                Console.Write("Tên khách hàng: ");
                string customerName = Console.ReadLine();

                var order = new Order(orderId, customerName, currentUser.Username);

                // Thêm món/combo vào đơn hàng
                while (true)
                {
                    Console.WriteLine("\n1. Thêm món ăn");
                    Console.WriteLine("2. Thêm combo");
                    Console.WriteLine("3. Xem đơn hàng hiện tại");
                    Console.WriteLine("4. Kết thúc");
                    Console.Write("Chọn: ");

                    string choice = Console.ReadLine();
                    if (choice == "4") break;

                    if (choice == "1")
                    {
                        AddDishToOrder(order);
                    }
                    else if (choice == "2")
                    {
                        AddComboToOrder(order);
                    }
                    else if (choice == "3")
                    {
                        ShowCurrentOrder(order);
                    }
                }

                if (order.Items.Count == 0)
                {
                    Console.WriteLine("Đơn hàng phải có ít nhất 1 món!");
                    Console.ReadKey();
                    return;
                }

                orders[orderId] = order;

                // Cập nhật số lượt bán
                foreach (var item in order.Items)
                {
                    if (!item.IsCombo)
                    {
                        if (dishes.ContainsKey(item.ItemId))
                        {
                            dishes[item.ItemId].SalesCount += item.Quantity;
                        }
                    }
                    else
                    {
                        if (combos.ContainsKey(item.ItemId))
                        {
                            combos[item.ItemId].SalesCount += item.Quantity;
                        }
                    }
                }

                // Trừ nguyên liệu
                if (DeductIngredients(order))
                {
                    auditLogs.Add(new AuditLog(currentUser.Username, "CREATE_ORDER", "ORDER", orderId, $"Tạo đơn: {customerName} - {order.TotalAmount:N0}đ"));
                    SaveAllData();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n🎉 TẠO ĐƠN HÀNG THÀNH CÔNG!");
                    Console.WriteLine($"📋 Mã đơn: {orderId}");
                    Console.WriteLine($"👤 Khách hàng: {customerName}");
                    Console.WriteLine($"💰 Tổng tiền: {order.TotalAmount:N0}đ");
                    Console.WriteLine($"👨‍💼 Nhân viên: {currentUser.FullName}");
                    Console.ResetColor();

                    // Xuất hóa đơn
                    ExportOrderInvoice(order);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Không đủ nguyên liệu để thực hiện đơn hàng!");
                    Console.ResetColor();
                    orders.Remove(orderId);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void AddDishToOrder(Order order)
        {
            Console.WriteLine("\nDANH SÁCH MÓN ĂN:");
            DisplayDishesForOrder();

            Console.Write("Mã món ăn: ");
            string dishId = Console.ReadLine();

            if (!dishes.ContainsKey(dishId) || !dishes[dishId].IsAvailable)
            {
                Console.WriteLine("Món ăn không tồn tại hoặc không khả dụng!");
                return;
            }

            Console.Write("Số lượng: ");
            int quantity = int.Parse(Console.ReadLine());

            var orderItem = new OrderItem
            {
                ItemId = dishId,
                IsCombo = false,
                Quantity = quantity,
                UnitPrice = dishes[dishId].Price
            };

            order.Items.Add(orderItem);
            Console.WriteLine($"✅ Đã thêm {quantity} {dishes[dishId].Name} vào đơn hàng!");
        }

        private void AddComboToOrder(Order order)
        {
            Console.WriteLine("\nDANH SÁCH COMBO:");
            DisplayCombosForOrder();

            Console.Write("Mã combo: ");
            string comboId = Console.ReadLine();

            if (!combos.ContainsKey(comboId) || !combos[comboId].IsActive)
            {
                Console.WriteLine("Combo không tồn tại hoặc không khả dụng!");
                return;
            }

            Console.Write("Số lượng: ");
            int quantity = int.Parse(Console.ReadLine());

            combos[comboId].CalculateOriginalPrice(dishes);
            var orderItem = new OrderItem
            {
                ItemId = comboId,
                IsCombo = true,
                Quantity = quantity,
                UnitPrice = combos[comboId].FinalPrice
            };

            order.Items.Add(orderItem);
            Console.WriteLine($"✅ Đã thêm {quantity} {combos[comboId].Name} vào đơn hàng!");
        }

        private void DisplayDishesForOrder()
        {
            Console.WriteLine("─────────────────────────────────────────────────────────");
            foreach (var dish in dishes.Values.Where(d => d.IsAvailable).Take(20))
            {
                Console.WriteLine($"{dish.Id} - {dish.Name} - {dish.Price:N0}đ");
            }
        }

        private void DisplayCombosForOrder()
        {
            Console.WriteLine("─────────────────────────────────────────────────────────");
            foreach (var combo in combos.Values.Where(c => c.IsActive).Take(10))
            {
                combo.CalculateOriginalPrice(dishes);
                Console.WriteLine($"{combo.Id} - {combo.Name} - {combo.FinalPrice:N0}đ - {combo.DishIds.Count} món");
            }
        }

        private void ShowCurrentOrder(Order order)
        {
            Console.WriteLine("\n📋 ĐƠN HÀNG HIỆN TẠI:");
            Console.WriteLine("─────────────────────────────────────────────────────────");
            foreach (var item in order.Items)
            {
                string itemName = item.IsCombo ?
                    $"[COMBO] {combos[item.ItemId].Name}" :
                    dishes[item.ItemId].Name;
                Console.WriteLine($"- {itemName} x{item.Quantity} = {item.TotalPrice:N0}đ");
            }
            Console.WriteLine($"💰 TỔNG TIỀN: {order.TotalAmount:N0}đ");
            Console.WriteLine("─────────────────────────────────────────────────────────");
        }

        private bool DeductIngredients(Order order)
        {
            foreach (var item in order.Items)
            {
                if (!item.IsCombo)
                {
                    // Trừ nguyên liệu cho món ăn đơn lẻ
                    if (dishes.ContainsKey(item.ItemId))
                    {
                        var dish = dishes[item.ItemId];
                        foreach (var ing in dish.Ingredients)
                        {
                            if (!ingredients.ContainsKey(ing.Key) || ingredients[ing.Key].Quantity < ing.Value * item.Quantity)
                            {
                                return false;
                            }
                        }
                    }
                }
                else
                {
                    // Trừ nguyên liệu cho combo
                    if (combos.ContainsKey(item.ItemId))
                    {
                        var combo = combos[item.ItemId];
                        foreach (var dishId in combo.DishIds)
                        {
                            if (dishes.ContainsKey(dishId))
                            {
                                var dish = dishes[dishId];
                                foreach (var ing in dish.Ingredients)
                                {
                                    if (!ingredients.ContainsKey(ing.Key) || ingredients[ing.Key].Quantity < ing.Value * item.Quantity)
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
                    if (dishes.ContainsKey(item.ItemId))
                    {
                        var dish = dishes[item.ItemId];
                        foreach (var ing in dish.Ingredients)
                        {
                            ingredients[ing.Key].Quantity -= ing.Value * item.Quantity;
                        }
                    }
                }
                else
                {
                    if (combos.ContainsKey(item.ItemId))
                    {
                        var combo = combos[item.ItemId];
                        foreach (var dishId in combo.DishIds)
                        {
                            if (dishes.ContainsKey(dishId))
                            {
                                var dish = dishes[dishId];
                                foreach (var ing in dish.Ingredients)
                                {
                                    ingredients[ing.Key].Quantity -= ing.Value * item.Quantity;
                                }
                            }
                        }
                    }
                }
            }
            return true;
        }

        private void DisplayOrders(int page = 1, int pageSize = 20)
        {
            Console.Clear();
            var orderList = orders.Values.ToList();
            int totalPages = (int)Math.Ceiling(orderList.Count / (double)pageSize);

            // Khung tiêu đề
            string title = "DANH SÁCH ĐƠN HÀNG";
            int width = 75;
            int padding = (width - title.Length) / 2;

            Console.WriteLine(new string('═', width));
            Console.WriteLine(title.PadLeft(padding + title.Length).PadRight(width));
            Console.WriteLine(new string('═', width));

            // Cách bảng 2 dòng
            Console.WriteLine("\n\n");

            // Header bảng
            Console.WriteLine("{0,-20} | {1,-15} | {2,-6} | {3,-12} | {4,-12}",
                "Mã đơn", "Khách hàng", "Số món", "Tổng tiền", "Trạng thái");
            Console.WriteLine(new string('-', width));

            var pagedOrders = orderList.OrderByDescending(o => o.OrderDate)
                                       .Skip((page - 1) * pageSize)
                                       .Take(pageSize);

            foreach (var order in pagedOrders)
            {
                Console.Write("{0,-20} | {1,-15} | {2,-6} | {3,-12} | ",
                    order.Id,
                    TruncateString(order.CustomerName, 15),
                    order.Items.Count,
                    $"{order.TotalAmount:N0}đ");

                string status = GetStatusText(order.Status);
                switch (status.ToLower())
                {
                    case "hoàn thành":
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;
                    case "chờ xử lý":
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case "hủy":
                    case "đã hủy":
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    default:
                        Console.ResetColor();
                        break;
                }

                Console.WriteLine("{0,-12}", status);
                Console.ResetColor();
            }

            Console.WriteLine(new string('-', width));
            Console.WriteLine($"Trang {page}/{totalPages} - Tổng cộng: {orderList.Count} đơn hàng\n");

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


        private string GetStatusText(OrderStatus status)
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
            Console.Clear();
            Console.WriteLine("CẬP NHẬT TRẠNG THÁI ĐƠN HÀNG");
            Console.WriteLine("==============================");

            // Hiển thị danh sách đơn hàng để chọn
            DisplayOrdersSimple();

            Console.Write("\nNhập mã đơn hàng: ");
            string orderId = Console.ReadLine();

            if (!orders.ContainsKey(orderId))
            {
                Console.WriteLine("Đơn hàng không tồn tại!");
                Console.ReadKey();
                return;
            }

            var order = orders[orderId];

            Console.WriteLine($"\nThông tin đơn hàng:");
            Console.WriteLine($"- Mã đơn: {order.Id}");
            Console.WriteLine($"- Khách hàng: {order.CustomerName}");
            Console.WriteLine($"- Tổng tiền: {order.TotalAmount:N0}đ");
            Console.WriteLine($"- Trạng thái hiện tại: {GetStatusText(order.Status)}");

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
                    Console.WriteLine("Lựa chọn không hợp lệ!");
                    return;
            }

            order.Status = newStatus;
            auditLogs.Add(new AuditLog(currentUser.Username, "UPDATE_ORDER_STATUS", "ORDER", orderId, $"Cập nhật trạng thái: {newStatus}"));
            SaveAllData();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Cập nhật trạng thái thành công: {GetStatusText(newStatus)}");
            Console.ResetColor();
            Console.ReadKey();
        }

        private void DisplayOrdersSimple()
        {
            Console.WriteLine("\nDANH SÁCH ĐƠN HÀNG:");
            Console.WriteLine("─────────────────────────────────────────────────────────");
            foreach (var order in orders.Values.OrderByDescending(o => o.OrderDate).Take(20))
            {
                Console.WriteLine($"{order.Id} - {order.CustomerName} - {order.TotalAmount:N0}đ - {GetStatusText(order.Status)}");
            }
        }

        private void ShowOrderDetail()
        {
            Console.Clear();
            Console.WriteLine("CHI TIẾT ĐƠN HÀNG");
            Console.WriteLine("==================");

            // Hiển thị danh sách đơn hàng để chọn
            DisplayOrdersSimple();

            Console.Write("\nNhập mã đơn hàng: ");
            string orderId = Console.ReadLine();

            if (!orders.ContainsKey(orderId))
            {
                Console.WriteLine("Đơn hàng không tồn tại!");
                Console.ReadKey();
                return;
            }

            var order = orders[orderId];

            Console.WriteLine($"\nMã đơn: {order.Id}");
            Console.WriteLine($"Khách hàng: {order.CustomerName}");
            Console.WriteLine($"Nhân viên: {order.StaffUsername}");
            Console.WriteLine($"Ngày đặt: {order.OrderDate:dd/MM/yyyy HH:mm}");
            Console.WriteLine($"Trạng thái: {GetStatusText(order.Status)}");

            if (order.CompletedDate.HasValue)
            {
                Console.WriteLine($"Ngày hoàn thành: {order.CompletedDate.Value:dd/MM/yyyy HH:mm}");
            }

            Console.WriteLine("\nCHI TIẾT ĐƠN HÀNG:");
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║ {0,-30} {1,-10} {2,-15} {3,-10} ║",
                "Tên món/combo", "Số lượng", "Đơn giá", "Thành tiền");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

            foreach (var item in order.Items)
            {
                string itemName = "";
                if (item.IsCombo)
                {
                    if (combos.ContainsKey(item.ItemId))
                        itemName = "[COMBO] " + combos[item.ItemId].Name;
                    else
                        itemName = "[COMBO] Không xác định";
                }
                else
                {
                    if (dishes.ContainsKey(item.ItemId))
                        itemName = dishes[item.ItemId].Name;
                    else
                        itemName = "Món không xác định";
                }

                Console.WriteLine("║ {0,-30} {1,-10} {2,-15} {3,-10} ║",
                    TruncateString(itemName, 30),
                    item.Quantity,
                    $"{item.UnitPrice:N0}đ",
                    $"{item.TotalPrice:N0}đ");
            }
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-30} {1,-10} {2,-15} {3,-10} ║",
                "TỔNG CỘNG", "", "", $"{order.TotalAmount:N0}đ");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ShowOrderStatistics()
        {
            Console.Clear();
            Console.WriteLine("THỐNG KÊ ĐƠN HÀNG");
            Console.WriteLine("==================");

            var today = DateTime.Today;
            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var completedOrders = orders.Values.Where(o => o.Status == OrderStatus.Completed).ToList();
            var dailyOrders = completedOrders.Where(o => o.OrderDate.Date == today).ToList();
            var weeklyOrders = completedOrders.Where(o => o.OrderDate >= weekStart).ToList();
            var monthlyOrders = completedOrders.Where(o => o.OrderDate >= monthStart).ToList();

            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                      THỐNG KÊ DOANH THU                       ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-25} {1,-15} {2,-15} ║", "Thời gian", "Số đơn", "Doanh thu");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-25} {1,-15} {2,-15} ║", "Hôm nay", dailyOrders.Count, $"{dailyOrders.Sum(o => o.TotalAmount):N0}đ");
            Console.WriteLine("║ {0,-25} {1,-15} {2,-15} ║", "Tuần này", weeklyOrders.Count, $"{weeklyOrders.Sum(o => o.TotalAmount):N0}đ");
            Console.WriteLine("║ {0,-25} {1,-15} {2,-15} ║", "Tháng này", monthlyOrders.Count, $"{monthlyOrders.Sum(o => o.TotalAmount):N0}đ");
            Console.WriteLine("║ {0,-25} {1,-15} {2,-15} ║", "Tổng cộng", completedOrders.Count, $"{completedOrders.Sum(o => o.TotalAmount):N0}đ");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

            Console.WriteLine("\n📊 PHÂN BỔ TRẠNG THÁI ĐƠN HÀNG:");
            var statusGroups = orders.Values.GroupBy(o => o.Status);
            foreach (var group in statusGroups)
            {
                Console.WriteLine($"{GetStatusText(group.Key)}: {group.Count()} đơn");
            }

            // Top món bán chạy
            var topDishes = dishes.Values.OrderByDescending(d => d.SalesCount).Take(5);
            Console.WriteLine("\n🏆 TOP 5 MÓN BÁN CHẠY:");
            foreach (var dish in topDishes)
            {
                Console.WriteLine($"- {dish.Name}: {dish.SalesCount} lượt - {dish.Price * dish.SalesCount:N0}đ");
            }

            // Xuất báo cáo
            ExportOrderStatistics(completedOrders, dailyOrders, weeklyOrders, monthlyOrders);

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

                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("Mã đơn,Khách hàng,Nhân viên,Ngày đặt,Trạng thái,Tổng tiền,Số món");

                    foreach (var order in orders.Values.OrderByDescending(o => o.OrderDate))
                    {
                        writer.WriteLine($"{order.Id},{order.CustomerName},{order.StaffUsername},{order.OrderDate:dd/MM/yyyy HH:mm},{order.Status},{order.TotalAmount},{order.Items.Count}");
                    }
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Đã xuất danh sách đơn hàng: {fileName}");
                Console.ResetColor();

                auditLogs.Add(new AuditLog(currentUser.Username, "EXPORT_ORDERS", "SYSTEM", "", "Xuất danh sách đơn hàng"));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi khi xuất file: {ex.Message}");
                Console.ResetColor();
            }

            Console.ReadKey();
        }

        private void ExportOrderInvoice(Order order)
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"HoaDon_{order.Id}.txt";
                string filePath = Path.Combine(downloadPath, fileName);

                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("╔════════════════════════════════════════════════════════════════╗");
                    writer.WriteLine("║                         HÓA ĐƠN                               ║");
                    writer.WriteLine("╠════════════════════════════════════════════════════════════════╣");
                    writer.WriteLine($"║ Mã đơn: {order.Id,-50} ║");
                    writer.WriteLine($"║ Khách hàng: {order.CustomerName,-43} ║");
                    writer.WriteLine($"║ Nhân viên: {order.StaffUsername,-44} ║");
                    writer.WriteLine($"║ Ngày: {order.OrderDate:dd/MM/yyyy HH:mm,-41} ║");
                    writer.WriteLine("╠════════════════════════════════════════════════════════════════╣");
                    writer.WriteLine("║ Tên món/combo                  Số lượng   Đơn giá   Thành tiền║");
                    writer.WriteLine("╠════════════════════════════════════════════════════════════════╣");

                    foreach (var item in order.Items)
                    {
                        string itemName = item.IsCombo ?
                            $"[COMBO] {combos[item.ItemId].Name}" :
                            dishes[item.ItemId].Name;

                        writer.WriteLine($"║ {TruncateString(itemName, 30),-30} {item.Quantity,-10} {item.UnitPrice,-9} {item.TotalPrice,-10} ║");
                    }

                    writer.WriteLine("╠════════════════════════════════════════════════════════════════╣");
                    writer.WriteLine($"║ TỔNG CỘNG: {order.TotalAmount,45}đ ║");
                    writer.WriteLine("╚════════════════════════════════════════════════════════════════╝");
                    writer.WriteLine();
                    writer.WriteLine("           Cảm ơn quý khách và hẹn gặp lại!");
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"📄 Đã xuất hóa đơn: {fileName}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi xuất hóa đơn: {ex.Message}");
            }
        }

        // ==================== REPORT MANAGEMENT ====================
        private void ShowReportMenu()
        {
            while (true)
            {
                Console.Clear();
                DisplayHeader();
                Console.WriteLine("╔═════════════════════════════════════════════╗");
                Console.WriteLine("║           THỐNG KÊ & BÁO CÁO                ║");
                Console.WriteLine("╠═════════════════════════════════════════════╣");
                Console.WriteLine("║ 1. Thống kê món ăn theo nhóm                ║");
                Console.WriteLine("║ 2. Thống kê nguyên liệu                     ║");
                Console.WriteLine("║ 3. Thống kê doanh thu                       ║");
                Console.WriteLine("║ 4. Thống kê combo bán chạy                  ║");
                Console.WriteLine("║ 5. Xuất báo cáo tổng hợp                    ║");
                Console.WriteLine("║ 6. Xuất lịch sử thao tác                    ║");
                Console.WriteLine("║ 0. Quay lại                                 ║");
                Console.WriteLine("╚═════════════════════════════════════════════╝");
                Console.Write("Chọn chức năng: ");

                string choice = Console.ReadLine();
                switch (choice)
                {
                    case "1": ShowDishCategoryReport(); break;
                    case "2": ShowIngredientReport(); break;
                    case "3": ShowRevenueReport(); break;
                    case "4": ShowComboSalesReport(); break;
                    case "5": ExportComprehensiveReport(); break;
                    case "6": ExportAuditLogs(); break;
                    case "0": return;
                    default: Console.WriteLine("Lựa chọn không hợp lệ!"); Thread.Sleep(1000); break;
                }
            }
        }

        private void ShowDishCategoryReport()
        {
            Console.Clear();
            Console.WriteLine("THỐNG KÊ MÓN ĂN THEO NHÓM");
            Console.WriteLine("==========================");

            var categoryGroups = dishes.Values.GroupBy(d => d.Category)
                .Select(g => new { Category = g.Key, Count = g.Count(), TotalSales = g.Sum(d => d.SalesCount) })
                .OrderByDescending(g => g.Count);

            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                THỐNG KÊ MÓN ĂN THEO NHÓM                     ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-25} {1,-10} {2,-15} {3,-10} ║", "Nhóm món", "Số món", "Tổng lượt bán", "Tỷ lệ");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

            int totalDishes = dishes.Count;
            int totalSales = dishes.Values.Sum(d => d.SalesCount);

            foreach (var group in categoryGroups)
            {
                double percentage = totalDishes > 0 ? (double)group.Count / totalDishes * 100 : 0;
                Console.WriteLine("║ {0,-25} {1,-10} {2,-15} {3,-9} ║",
                    TruncateString(group.Category, 25),
                    group.Count,
                    group.TotalSales,
                    $"{percentage:0.0}%");
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

            // Xuất file báo cáo
            ExportDishCategoryReport(
                categoryGroups
                    .Select(c => (dynamic)c)
                    .ToList()
            );

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ShowIngredientReport()
        {
            Console.Clear();
            Console.WriteLine("THỐNG KÊ NGUYÊN LIỆU");
            Console.WriteLine("=====================");

            var lowStock = ingredients.Values.Where(ing => ing.IsLowStock).ToList();
            var sufficientStock = ingredients.Values.Where(ing => !ing.IsLowStock).Take(10).ToList();

            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                  NGUYÊN LIỆU SẮP HẾT                         ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

            if (lowStock.Any())
            {
                foreach (var ing in lowStock.Take(10))
                {
                    Console.WriteLine("║ ⚠️  {0,-25} {1,-8} {2,-8} {3,-10} ║",
                        TruncateString(ing.Name, 25),
                        $"{ing.Quantity} {ing.Unit}",
                        $"{ing.MinQuantity} {ing.Unit}",
                        $"Cần: {ing.MinQuantity - ing.Quantity}");
                }
            }
            else
            {
                Console.WriteLine("║ {0,-58} ║", "✅ Không có nguyên liệu nào sắp hết");
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

            Console.WriteLine("\n📊 TỔNG QUAN NGUYÊN LIỆU:");
            Console.WriteLine($"- Tổng số nguyên liệu: {ingredients.Count}");
            Console.WriteLine($"- Nguyên liệu sắp hết: {lowStock.Count}");
            Console.WriteLine($"- Nguyên liệu đủ: {ingredients.Count - lowStock.Count}");

            // Xuất file báo cáo
            ExportIngredientReport(lowStock);

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ShowRevenueReport()
        {
            Console.Clear();
            Console.WriteLine("THỐNG KÊ DOANH THU");
            Console.WriteLine("===================");

            var completedOrders = orders.Values.Where(o => o.Status == OrderStatus.Completed).ToList();
            var today = DateTime.Today;

            var dailyRevenue = completedOrders.Where(o => o.CompletedDate?.Date == today).Sum(o => o.TotalAmount);
            var weeklyRevenue = completedOrders.Where(o => o.CompletedDate?.Date >= today.AddDays(-7)).Sum(o => o.TotalAmount);
            var monthlyRevenue = completedOrders.Where(o => o.CompletedDate?.Date >= today.AddDays(-30)).Sum(o => o.TotalAmount);
            var totalRevenue = completedOrders.Sum(o => o.TotalAmount);

            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                      THỐNG KÊ DOANH THU                       ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-25} {1,-30} ║", "Hôm nay:", $"{dailyRevenue:N0}đ");
            Console.WriteLine("║ {0,-25} {1,-30} ║", "7 ngày qua:", $"{weeklyRevenue:N0}đ");
            Console.WriteLine("║ {0,-25} {1,-30} ║", "30 ngày qua:", $"{monthlyRevenue:N0}đ");
            Console.WriteLine("║ {0,-25} {1,-30} ║", "Tổng doanh thu:", $"{totalRevenue:N0}đ");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

            // Top món bán chạy
            var topDishes = dishes.Values.OrderByDescending(d => d.SalesCount).Take(5);
            Console.WriteLine("\n🏆 TOP 5 MÓN BÁN CHẠY:");
            foreach (var dish in topDishes)
            {
                decimal revenue = dish.Price * dish.SalesCount;
                Console.WriteLine($"- {dish.Name}: {dish.SalesCount} lượt - {revenue:N0}đ");
            }

            // Xuất file báo cáo
            ExportRevenueReport(completedOrders, dailyRevenue, weeklyRevenue, monthlyRevenue, totalRevenue);

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

                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("BÁO CÁO TỔNG HỢP HỆ THỐNG NHÀ HÀNG");
                    writer.WriteLine($"Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                    writer.WriteLine("==========================================");
                    writer.WriteLine();

                    // Thống kê tổng quan
                    writer.WriteLine("📊 TỔNG QUAN HỆ THỐNG:");
                    writer.WriteLine($"- Tổng số món ăn: {dishes.Count}");
                    writer.WriteLine($"- Tổng số nguyên liệu: {ingredients.Count}");
                    writer.WriteLine($"- Tổng số combo: {combos.Count}");
                    writer.WriteLine($"- Tổng số đơn hàng: {orders.Count}");
                    writer.WriteLine();

                    // Thống kê doanh thu
                    var completedOrders = orders.Values.Where(o => o.Status == OrderStatus.Completed).ToList();
                    var totalRevenue = completedOrders.Sum(o => o.TotalAmount);
                    writer.WriteLine("💰 THỐNG KÊ DOANH THU:");
                    writer.WriteLine($"- Tổng doanh thu: {totalRevenue:N0}đ");
                    writer.WriteLine($"- Số đơn hoàn thành: {completedOrders.Count}");
                    writer.WriteLine($"- Đơn hàng trung bình: {(completedOrders.Any() ? totalRevenue / completedOrders.Count : 0):N0}đ");
                    writer.WriteLine();

                    // Top món bán chạy
                    writer.WriteLine("🏆 TOP 5 MÓN BÁN CHẠY:");
                    var topDishes = dishes.Values.OrderByDescending(d => d.SalesCount).Take(5);
                    foreach (var dish in topDishes)
                    {
                        writer.WriteLine($"- {dish.Name}: {dish.SalesCount} lượt - {dish.Price * dish.SalesCount:N0}đ");
                    }
                    writer.WriteLine();

                    // Cảnh báo tồn kho
                    var lowStock = ingredients.Values.Where(ing => ing.IsLowStock).ToList();
                    writer.WriteLine("⚠️  CẢNH BÁO TỒN KHO:");
                    if (lowStock.Any())
                    {
                        foreach (var ing in lowStock.Take(10))
                        {
                            writer.WriteLine($"- {ing.Name}: {ing.Quantity} {ing.Unit} (Tối thiểu: {ing.MinQuantity} {ing.Unit})");
                        }
                    }
                    else
                    {
                        writer.WriteLine("- Không có nguyên liệu nào sắp hết");
                    }
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Đã xuất báo cáo tổng hợp: {fileName}");
                Console.ResetColor();

                auditLogs.Add(new AuditLog(currentUser.Username, "EXPORT_COMPREHENSIVE_REPORT", "SYSTEM", "", "Xuất báo cáo tổng hợp"));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi khi xuất báo cáo: {ex.Message}");
                Console.ResetColor();
            }

            Console.ReadKey();
        }

        private void ExportAuditLogs()
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"LichSuThaoTac_{DateTime.Now:yyyyMMddHHmmss}.csv";
                string filePath = Path.Combine(downloadPath, fileName);

                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("Thời gian,Người dùng,Thao tác,Loại thực thể,Mã thực thể,Chi tiết");

                    foreach (var log in auditLogs.OrderByDescending(a => a.Timestamp))
                    {
                        writer.WriteLine($"{log.Timestamp:dd/MM/yyyy HH:mm},{log.Username},{log.Action},{log.EntityType},{log.EntityId},{log.Details}");
                    }
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Đã xuất lịch sử thao tác: {fileName}");
                Console.ResetColor();

                auditLogs.Add(new AuditLog(currentUser.Username, "EXPORT_AUDIT_LOGS", "SYSTEM", "", "Xuất lịch sử thao tác"));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi khi xuất file: {ex.Message}");
                Console.ResetColor();
            }

            Console.ReadKey();
        }

        // ==================== CÁC PHƯƠNG THỨC XUẤT FILE BỔ SUNG ====================
        private void ExportComboReport(List<Combo> topCombos)
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"BaoCaoCombo_{DateTime.Now:yyyyMMddHHmmss}.txt";
                string filePath = Path.Combine(downloadPath, fileName);

                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("BÁO CÁO COMBO BÁN CHẠY");
                    writer.WriteLine($"Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                    writer.WriteLine("==========================================");
                    writer.WriteLine();

                    foreach (var combo in topCombos)
                    {
                        combo.CalculateOriginalPrice(dishes);
                        decimal revenue = combo.FinalPrice * combo.SalesCount;
                        writer.WriteLine($"{combo.Name}:");
                        writer.WriteLine($"  - Số lượt bán: {combo.SalesCount}");
                        writer.WriteLine($"  - Doanh thu: {revenue:N0}đ");
                        writer.WriteLine($"  - Giá bán: {combo.FinalPrice:N0}đ");
                        writer.WriteLine();
                    }
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Đã xuất báo cáo combo: {fileName}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi xuất báo cáo combo: {ex.Message}");
            }
        }

        private void ExportOrderStatistics(List<Order> completedOrders, List<Order> dailyOrders, List<Order> weeklyOrders, List<Order> monthlyOrders)
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"ThongKeDonHang_{DateTime.Now:yyyyMMddHHmmss}.txt";
                string filePath = Path.Combine(downloadPath, fileName);

                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("THỐNG KÊ ĐƠN HÀNG");
                    writer.WriteLine($"Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                    writer.WriteLine("==========================================");
                    writer.WriteLine();

                    writer.WriteLine($"Doanh thu hôm nay: {dailyOrders.Sum(o => o.TotalAmount):N0}đ ({dailyOrders.Count} đơn)");
                    writer.WriteLine($"Doanh thu tuần này: {weeklyOrders.Sum(o => o.TotalAmount):N0}đ ({weeklyOrders.Count} đơn)");
                    writer.WriteLine($"Doanh thu tháng này: {monthlyOrders.Sum(o => o.TotalAmount):N0}đ ({monthlyOrders.Count} đơn)");
                    writer.WriteLine($"Tổng doanh thu: {completedOrders.Sum(o => o.TotalAmount):N0}đ ({completedOrders.Count} đơn)");
                    writer.WriteLine();

                    writer.WriteLine("TOP 5 MÓN BÁN CHẠY:");
                    var topDishes = dishes.Values.OrderByDescending(d => d.SalesCount).Take(5);
                    foreach (var dish in topDishes)
                    {
                        writer.WriteLine($"- {dish.Name}: {dish.SalesCount} lượt");
                    }
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Đã xuất thống kê đơn hàng: {fileName}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi xuất thống kê: {ex.Message}");
            }
        }

        private void ExportDishCategoryReport(List<dynamic> categoryGroups)
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"BaoCaoNhomMon_{DateTime.Now:yyyyMMddHHmmss}.txt";
                string filePath = Path.Combine(downloadPath, fileName);

                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("BÁO CÁO PHÂN BỔ MÓN ĂN THEO NHÓM");
                    writer.WriteLine($"Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                    writer.WriteLine("==========================================");
                    writer.WriteLine();

                    foreach (var group in categoryGroups)
                    {
                        writer.WriteLine($"{group.Category}: {group.Count} món ({group.TotalSales} lượt bán)");
                    }
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Đã xuất báo cáo nhóm món: {fileName}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi xuất báo cáo: {ex.Message}");
            }
        }

        private void ExportIngredientReport(List<Ingredient> lowStockIngredients)
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"BaoCaoNguyenLieu_{DateTime.Now:yyyyMMddHHmmss}.txt";
                string filePath = Path.Combine(downloadPath, fileName);

                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("BÁO CÁO NGUYÊN LIỆU");
                    writer.WriteLine($"Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                    writer.WriteLine("==========================================");
                    writer.WriteLine();

                    writer.WriteLine($"Tổng số nguyên liệu: {ingredients.Count}");
                    writer.WriteLine($"Nguyên liệu sắp hết: {lowStockIngredients.Count}");
                    writer.WriteLine();

                    if (lowStockIngredients.Any())
                    {
                        writer.WriteLine("DANH SÁCH NGUYÊN LIỆU CẦN NHẬP:");
                        foreach (var ing in lowStockIngredients)
                        {
                            writer.WriteLine($"- {ing.Name}: {ing.Quantity} {ing.Unit} (Tối thiểu: {ing.MinQuantity} {ing.Unit}) - Cần: {ing.MinQuantity - ing.Quantity} {ing.Unit}");
                        }
                    }
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Đã xuất báo cáo nguyên liệu: {fileName}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi xuất báo cáo: {ex.Message}");
            }
        }

        private void ExportRevenueReport(List<Order> completedOrders, decimal dailyRevenue, decimal weeklyRevenue, decimal monthlyRevenue, decimal totalRevenue)
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"BaoCaoDoanhThu_{DateTime.Now:yyyyMMddHHmmss}.txt";
                string filePath = Path.Combine(downloadPath, fileName);

                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("BÁO CÁO DOANH THU");
                    writer.WriteLine($"Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                    writer.WriteLine("==========================================");
                    writer.WriteLine();

                    writer.WriteLine($"Doanh thu hôm nay: {dailyRevenue:N0}đ");
                    writer.WriteLine($"Doanh thu 7 ngày: {weeklyRevenue:N0}đ");
                    writer.WriteLine($"Doanh thu 30 ngày: {monthlyRevenue:N0}đ");
                    writer.WriteLine($"Tổng doanh thu: {totalRevenue:N0}đ");
                    writer.WriteLine();

                    writer.WriteLine("TOP MÓN BÁN CHẠY:");
                    var topDishes = dishes.Values.OrderByDescending(d => d.SalesCount).Take(10);
                    foreach (var dish in topDishes)
                    {
                        writer.WriteLine($"- {dish.Name}: {dish.SalesCount} lượt - {dish.Price * dish.SalesCount:N0}đ");
                    }
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Đã xuất báo cáo doanh thu: {fileName}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi xuất báo cáo: {ex.Message}");
            }
        }

        // ==================== UTILITY METHODS ====================
        private bool CheckDishIngredients(Dish dish)
        {
            foreach (var ing in dish.Ingredients)
            {
                if (!ingredients.ContainsKey(ing.Key) || ingredients[ing.Key].Quantity < ing.Value)
                    return false;
            }
            return true;
        }

        // ==================== USER MANAGEMENT ====================
        private void ShowUserManagementMenu()
        {
            while (true)
            {
                Console.Clear();
                DisplayHeader();
                Console.WriteLine("╔═════════════════════════════════════════════╗");
                Console.WriteLine("║          QUẢN LÝ NGƯỜI DÙNG                 ║");
                Console.WriteLine("╠═════════════════════════════════════════════╣");
                Console.WriteLine("║ 1. Xem danh sách người dùng                 ║");
                Console.WriteLine("║ 2. Thêm người dùng mới                      ║");
                Console.WriteLine("║ 3. Cập nhật người dùng                      ║");
                Console.WriteLine("║ 4. Xóa người dùng                           ║");
                Console.WriteLine("║ 5. Xem lịch sử thao tác                     ║");
                Console.WriteLine("║ 0. Quay lại                                 ║");
                Console.WriteLine("╚═════════════════════════════════════════════╝");
                Console.Write("Chọn chức năng: ");

                string choice = Console.ReadLine();
                switch (choice)
                {
                    case "1": DisplayUsers(); break;
                    case "2": AddUser(); break;
                    case "3": UpdateUser(); break;
                    case "4": DeleteUser(); break;
                    case "5": ShowAuditLogs(); break;
                    case "0": return;
                    default: Console.WriteLine("Lựa chọn không hợp lệ!"); Thread.Sleep(1000); break;
                }
            }
        }

        private void DisplayUsers()
        {
            Console.Clear();
            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                               DANH SÁCH NGƯỜI DÙNG                          ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-15} {1,-25} {2,-12} {3,-15} {4,-10} ║",
                "Tên đăng nhập", "Họ tên", "Vai trò", "Ngày tạo", "Trạng thái");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

            foreach (var user in users.Values)
            {
                Console.WriteLine("║ {0,-15} {1,-25} {2,-12} {3,-15} {4,-10} ║",
                    user.Username,
                    TruncateString(user.FullName, 25),
                    user.Role,
                    user.CreatedDate.ToString("dd/MM/yyyy"),
                    user.IsActive ? "Hoạt động" : "Vô hiệu hóa");
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void AddUser()
        {
            Console.Clear();
            Console.WriteLine("THÊM NGƯỜI DÙNG MỚI");
            Console.WriteLine("====================");

            try
            {
                Console.Write("Tên đăng nhập: ");
                string username = Console.ReadLine();

                if (users.ContainsKey(username))
                {
                    Console.WriteLine("Tên đăng nhập đã tồn tại!");
                    Console.ReadKey();
                    return;
                }

                Console.Write("Họ tên: ");
                string fullName = Console.ReadLine();

                Console.WriteLine("Vai trò:");
                Console.WriteLine("1. Admin");
                Console.WriteLine("2. Quản lý");
                Console.WriteLine("3. Nhân viên");
                Console.Write("Chọn: ");
                string roleChoice = Console.ReadLine();

                UserRole role = UserRole.Staff;
                switch (roleChoice)
                {
                    case "1": role = UserRole.Admin; break;
                    case "2": role = UserRole.Manager; break;
                    case "3": role = UserRole.Staff; break;
                    default:
                        Console.WriteLine("Lựa chọn không hợp lệ, mặc định là Nhân viên!");
                        break;
                }

                string password = SecurityService.GenerateRandomPassword();
                string passwordHash = SecurityService.HashPassword(password);

                var user = new User(username, passwordHash, role, fullName);
                users[username] = user;

                auditLogs.Add(new AuditLog(currentUser.Username, "ADD_USER", "USER", username, $"Thêm người dùng: {fullName}"));
                SaveAllData();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Thêm người dùng thành công!");
                Console.WriteLine($"Mật khẩu mặc định: {password}");
                Console.WriteLine("Hãy yêu cầu người dùng đổi mật khẩu ngay sau khi đăng nhập!");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void UpdateUser()
        {
            Console.Clear();
            Console.WriteLine("CẬP NHẬT NGƯỜI DÙNG");
            Console.WriteLine("====================");

            // Hiển thị danh sách người dùng
            DisplayUsersSimple();

            try
            {
                Console.Write("\nNhập tên đăng nhập cần cập nhật: ");
                string username = Console.ReadLine();

                if (!users.ContainsKey(username))
                {
                    Console.WriteLine("Người dùng không tồn tại!");
                    Console.ReadKey();
                    return;
                }

                var user = users[username];

                Console.Write($"Họ tên ({user.FullName}): ");
                string fullName = Console.ReadLine();
                if (!string.IsNullOrEmpty(fullName)) user.FullName = fullName;

                Console.WriteLine($"Vai trò hiện tại: {user.Role}");
                Console.WriteLine("1. Admin");
                Console.WriteLine("2. Quản lý");
                Console.WriteLine("3. Nhân viên");
                Console.Write("Chọn vai trò mới (để trống nếu không đổi): ");
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

                auditLogs.Add(new AuditLog(currentUser.Username, "UPDATE_USER", "USER", username, $"Cập nhật người dùng: {user.FullName}"));
                SaveAllData();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Cập nhật người dùng thành công!");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void DeleteUser()
        {
            Console.Clear();
            Console.WriteLine("XÓA NGƯỜI DÙNG");
            Console.WriteLine("==============");

            // Hiển thị danh sách người dùng
            DisplayUsersSimple();

            try
            {
                Console.Write("\nNhập tên đăng nhập cần xóa: ");
                string username = Console.ReadLine();

                if (!users.ContainsKey(username))
                {
                    Console.WriteLine("Người dùng không tồn tại!");
                    Console.ReadKey();
                    return;
                }

                if (username == currentUser.Username)
                {
                    Console.WriteLine("Không thể xóa chính tài khoản đang đăng nhập!");
                    Console.ReadKey();
                    return;
                }

                var user = users[username];

                Console.WriteLine($"\nThông tin người dùng:");
                Console.WriteLine($"- Tên đăng nhập: {user.Username}");
                Console.WriteLine($"- Họ tên: {user.FullName}");
                Console.WriteLine($"- Vai trò: {user.Role}");

                Console.WriteLine($"\nBạn có chắc chắn muốn xóa người dùng '{user.FullName}'? (y/n)");
                string confirm = Console.ReadLine();

                if (confirm.ToLower() == "y")
                {
                    users.Remove(username);
                    auditLogs.Add(new AuditLog(currentUser.Username, "DELETE_USER", "USER", username, $"Xóa người dùng: {user.FullName}"));
                    SaveAllData();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Xóa người dùng thành công!");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lỗi: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void DisplayUsersSimple()
        {
            Console.WriteLine("\nDANH SÁCH NGƯỜI DÙNG:");
            Console.WriteLine("─────────────────────────────────────────────────────────");
            foreach (var user in users.Values)
            {
                Console.WriteLine($"{user.Username} - {user.FullName} - {user.Role} - {(user.IsActive ? "Hoạt động" : "Vô hiệu")}");
            }
        }

        private void ShowAuditLogs()
        {
            Console.Clear();
            Console.WriteLine("LỊCH SỬ THAO TÁC");
            Console.WriteLine("================");

            var recentLogs = auditLogs.OrderByDescending(a => a.Timestamp).Take(50);

            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                               LỊCH SỬ THAO TÁC                               ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ {0,-16} {1,-12} {1,-10} {2,-10} {3,-20} ║",
                "Thời gian", "Người dùng", "Thao tác", "Thực thể", "Chi tiết");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

            foreach (var log in recentLogs)
            {
                Console.WriteLine("║ {0,-16} {1,-12} {2,-10} {3,-10} {4,-20} ║",
                    log.Timestamp.ToString("dd/MM HH:mm"),
                    log.Username,
                    log.Action,
                    $"{log.EntityType}:{log.EntityId}",
                    TruncateString(log.Details, 20));
            }
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        // ==================== UTILITY MENU ====================
        private void ShowUtilityMenu()
        {
            while (true)
            {
                Console.Clear();
                DisplayHeader();
                Console.WriteLine("╔═════════════════════════════════════════════╗");
                Console.WriteLine("║            TIỆN ÍCH & CẢNH BÁO              ║");
                Console.WriteLine("╠═════════════════════════════════════════════╣");
                Console.WriteLine("║ 1. Kiểm tra cảnh báo tồn kho                ║");
                Console.WriteLine("║ 2. Tìm kiếm mờ (Fuzzy Search)               ║");
                Console.WriteLine("║ 3. Gợi ý món ăn thay thế                    ║");
                Console.WriteLine("║ 4. Backup dữ liệu                           ║");
                Console.WriteLine("║ 5. Restore dữ liệu                          ║");
                Console.WriteLine("║ 6. Xuất lịch sử thao tác                    ║");
                Console.WriteLine("║ 7. Xuất toàn bộ thông tin hệ thống          ║");
                Console.WriteLine("║ 0. Quay lại                                 ║");
                Console.WriteLine("╚═════════════════════════════════════════════╝");
                Console.Write("Chọn chức năng (X để thoát): ");

                string choice = Console.ReadLine();
                if (choice.ToLower() == "x") return;

                switch (choice)
                {
                    case "1": ShowInventoryWarnings(); break;
                    case "2": FuzzySearch(); break;
                    case "3": SuggestAlternativeDishes(); break;
                    case "4": BackupData(); break;
                    case "5": RestoreData(); break;
                    case "6": ExportAuditLogsWithPath(); break;
                    case "7": ExportAllSystemData(); break;
                    case "0": return;
                    default: Console.WriteLine("Lựa chọn không hợp lệ!"); Thread.Sleep(1000); break;
                }
            }
        }

        private void ExportAuditLogsWithPath()
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string fileName = $"LichSuThaoTac_{DateTime.Now:yyyyMMddHHmmss}.csv";
                string filePath = Path.Combine(downloadPath, fileName);

                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("Thời gian,Người dùng,Thao tác,Loại thực thể,Mã thực thể,Chi tiết");

                    foreach (var log in auditLogs.OrderByDescending(a => a.Timestamp))
                    {
                        writer.WriteLine($"{log.Timestamp:dd/MM/yyyy HH:mm},{log.Username},{log.Action},{log.EntityType},{log.EntityId},{log.Details}");
                    }
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✅ Đã xuất lịch sử thao tác!");
                Console.WriteLine($"📁 Đường dẫn: {filePath}");
                Console.ResetColor();

                auditLogs.Add(new AuditLog(currentUser.Username, "EXPORT_AUDIT_LOGS", "SYSTEM", "", "Xuất lịch sử thao tác"));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Lỗi khi xuất file: {ex.Message}");
                Console.ResetColor();
            }

            Console.ReadKey();
        }

        private void FuzzySearch()
        {
            Console.Clear();
            Console.WriteLine("TÌM KIẾM MỜ (FUZZY SEARCH)");
            Console.WriteLine("===========================");

            Console.Write("Nhập từ khóa tìm kiếm: ");
            string keyword = Console.ReadLine().ToLower();

            if (string.IsNullOrEmpty(keyword))
            {
                Console.WriteLine("Vui lòng nhập từ khóa tìm kiếm!");
                Console.ReadKey();
                return;
            }

            // Tìm kiếm gần đúng trong tên món ăn
            var dishResults = dishes.Values.Where(d =>
                CalculateLevenshteinDistance(d.Name.ToLower(), keyword) <= 2 ||
                d.Name.ToLower().Contains(keyword)).Take(10).ToList();

            // Tìm kiếm trong nguyên liệu
            var ingredientResults = ingredients.Values.Where(ing =>
                CalculateLevenshteinDistance(ing.Name.ToLower(), keyword) <= 2 ||
                ing.Name.ToLower().Contains(keyword)).Take(10).ToList();

            Console.WriteLine($"\n🔍 KẾT QUẢ TÌM KIẾM CHO '{keyword}':");

            if (dishResults.Any())
            {
                Console.WriteLine("\n🍽️  MÓN ĂN:");
                foreach (var dish in dishResults)
                {
                    int distance = CalculateLevenshteinDistance(dish.Name.ToLower(), keyword);
                    int similarity = 100 - distance * 25;
                    Console.WriteLine($"- {dish.Name} (độ tương đồng: {similarity}%) - {dish.Price:N0}đ");
                }
            }

            if (ingredientResults.Any())
            {
                Console.WriteLine("\n🥬 NGUYÊN LIỆU:");
                foreach (var ing in ingredientResults)
                {
                    int distance = CalculateLevenshteinDistance(ing.Name.ToLower(), keyword);
                    int similarity = 100 - distance * 25;
                    Console.WriteLine($"- {ing.Name} (độ tương đồng: {similarity}%) - {ing.Quantity} {ing.Unit}");
                }
            }

            if (!dishResults.Any() && !ingredientResults.Any())
            {
                Console.WriteLine("Không tìm thấy kết quả nào phù hợp!");
            }

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
            Console.Clear();
            Console.WriteLine("GỢI Ý MÓN ĂN THAY THẾ");
            Console.WriteLine("======================");

            // Hiển thị danh sách món ăn để chọn
            DisplayDishesSimple();

            Console.Write("\nNhập mã món ăn: ");
            string dishId = Console.ReadLine();

            if (!dishes.ContainsKey(dishId))
            {
                Console.WriteLine("Món ăn không tồn tại!");
                Console.ReadKey();
                return;
            }

            var originalDish = dishes[dishId];

            // Sửa lỗi: Thay đổi 0.3 thành 0.3m
            var alternatives = dishes.Values.Where(d =>
                d.Id != dishId &&
                d.Category == originalDish.Category &&
                Math.Abs(d.Price - originalDish.Price) <= originalDish.Price * 0.3m && // Sửa thành 0.3m
                CheckDishIngredients(d)).Take(5).ToList();

            if (alternatives.Any())
            {
                Console.WriteLine($"\n💡 Gợi ý thay thế cho '{originalDish.Name}':");
                foreach (var alt in alternatives)
                {
                    decimal priceDiff = alt.Price - originalDish.Price;
                    string diffText = priceDiff > 0 ? $"(+{priceDiff:N0}đ)" :
                                     priceDiff < 0 ? $"({priceDiff:N0}đ)" : "(bằng giá)";
                    string status = CheckDishIngredients(alt) ? "✅ Có đủ nguyên liệu" : "⚠️ Thiếu nguyên liệu";

                    Console.WriteLine($"- {alt.Name} ({alt.Price:N0}đ) {diffText} - {status}");
                }
            }
            else
            {
                Console.WriteLine("Không có món ăn thay thế phù hợp!");

                // Gợi ý các món cùng nhóm không xét giá
                var sameCategory = dishes.Values.Where(d =>
                    d.Id != dishId &&
                    d.Category == originalDish.Category &&
                    CheckDishIngredients(d))
                    .Take(3).ToList();

                if (sameCategory.Any())
                {
                    Console.WriteLine("\n🍽️  Món cùng nhóm có sẵn:");
                    foreach (var dish in sameCategory)
                    {
                        Console.WriteLine($"- {dish.Name} ({dish.Price:N0}đ)");
                    }
                }
            }

            Console.WriteLine("\nNhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void BackupData()
        {
            try
            {
                string backupFolder = Path.Combine(DATA_FOLDER, "Backup");
                if (!Directory.Exists(backupFolder))
                {
                    Directory.CreateDirectory(backupFolder);
                }

                string backupFile = Path.Combine(backupFolder, $"backup_{DateTime.Now:yyyyMMddHHmmss}.zip");
                SaveAllData(); // Lưu dữ liệu hiện tại trước

                // Sao chép các file dữ liệu
                string[] dataFiles = { "users.json", "ingredients.json", "dishes.json", "combos.json", "orders.json", "audit_logs.json" };

                Console.WriteLine("Đang sao lưu dữ liệu...");
                foreach (string file in dataFiles)
                {
                    string source = Path.Combine(DATA_FOLDER, file);
                    if (File.Exists(source))
                    {
                        string dest = Path.Combine(backupFolder, Path.GetFileNameWithoutExtension(file) + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".json");
                        File.Copy(source, dest);
                    }
                }

                // Sao lưu sang thư mục Downloads
                string downloadBackup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER, $"backup_{DateTime.Now:yyyyMMddHHmmss}");
                if (!Directory.Exists(downloadBackup))
                {
                    Directory.CreateDirectory(downloadBackup);
                }

                foreach (string file in dataFiles)
                {
                    string source = Path.Combine(DATA_FOLDER, file);
                    if (File.Exists(source))
                    {
                        File.Copy(source, Path.Combine(downloadBackup, file));
                    }
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✅ Sao lưu dữ liệu thành công!");
                Console.WriteLine($"📁 Backup nội bộ: {backupFolder}");
                Console.WriteLine($"📁 Backup Downloads: {downloadBackup}");
                Console.ResetColor();

                auditLogs.Add(new AuditLog(currentUser.Username, "BACKUP_DATA", "SYSTEM", "", "Sao lưu toàn bộ dữ liệu"));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Lỗi khi sao lưu: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void RestoreData()
        {
            try
            {
                string backupFolder = Path.Combine(DATA_FOLDER, "Backup");
                if (!Directory.Exists(backupFolder))
                {
                    Console.WriteLine("Thư mục backup không tồn tại!");
                    Console.ReadKey();
                    return;
                }

                var backupFiles = Directory.GetFiles(backupFolder, "*.json")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                if (!backupFiles.Any())
                {
                    Console.WriteLine("Không có file backup nào!");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine("📂 Danh sách file backup:");
                for (int i = 0; i < Math.Min(backupFiles.Count, 10); i++)
                {
                    Console.WriteLine($"{i + 1}. {backupFiles[i].Name} ({backupFiles[i].LastWriteTime:dd/MM/yyyy HH:mm})");
                }

                Console.Write("Chọn file để restore: ");
                if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= backupFiles.Count)
                {
                    string selectedFile = backupFiles[choice - 1].FullName;
                    string fileName = Path.GetFileName(selectedFile);

                    Console.WriteLine($"Bạn có chắc chắn muốn khôi phục từ file '{fileName}'? (y/n)");
                    if (Console.ReadLine().ToLower() == "y")
                    {
                        // Xác định loại dữ liệu từ tên file
                        if (fileName.Contains("users"))
                        {
                            var data = LoadData<Dictionary<string, User>>(selectedFile);
                            if (data != null) users = data;
                        }
                        else if (fileName.Contains("ingredients"))
                        {
                            var data = LoadData<Dictionary<string, Ingredient>>(selectedFile);
                            if (data != null) ingredients = data;
                        }
                        else if (fileName.Contains("dishes"))
                        {
                            var data = LoadData<Dictionary<string, Dish>>(selectedFile);
                            if (data != null) dishes = data;
                        }
                        else if (fileName.Contains("combos"))
                        {
                            var data = LoadData<Dictionary<string, Combo>>(selectedFile);
                            if (data != null) combos = data;
                        }
                        else if (fileName.Contains("orders"))
                        {
                            var data = LoadData<Dictionary<string, Order>>(selectedFile);
                            if (data != null) orders = data;
                        }
                        else if (fileName.Contains("audit_logs"))
                        {
                            var data = LoadData<List<AuditLog>>(selectedFile);
                            if (data != null) auditLogs = data;
                        }

                        SaveAllData(); // Lưu dữ liệu đã khôi phục

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("✅ Khôi phục dữ liệu thành công!");
                        Console.ResetColor();

                        auditLogs.Add(new AuditLog(currentUser.Username, "RESTORE_DATA", "SYSTEM", "", $"Khôi phục từ file: {fileName}"));
                    }
                }
                else
                {
                    Console.WriteLine("Lựa chọn không hợp lệ!");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Lỗi khi khôi phục: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ExportAllData()
        {
            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string exportFolder = Path.Combine(downloadPath, $"Export_Data_{DateTime.Now:yyyyMMddHHmmss}");

                if (!Directory.Exists(exportFolder))
                {
                    Directory.CreateDirectory(exportFolder);
                }

                // Xuất tất cả các loại dữ liệu
                SaveData(Path.Combine(exportFolder, "users.json"), users);
                SaveData(Path.Combine(exportFolder, "ingredients.json"), ingredients);
                SaveData(Path.Combine(exportFolder, "dishes.json"), dishes);
                SaveData(Path.Combine(exportFolder, "combos.json"), combos);
                SaveData(Path.Combine(exportFolder, "orders.json"), orders);
                SaveData(Path.Combine(exportFolder, "audit_logs.json"), auditLogs);

                // Xuất báo cáo tổng hợp
                ExportComprehensiveReportToFolder(exportFolder);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✅ Xuất toàn bộ dữ liệu thành công!");
                Console.WriteLine($"📁 Thư mục: {exportFolder}");
                Console.ResetColor();

                auditLogs.Add(new AuditLog(currentUser.Username, "EXPORT_ALL_DATA", "SYSTEM", "", "Xuất toàn bộ dữ liệu"));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Lỗi khi xuất dữ liệu: {ex.Message}");
                Console.ResetColor();
            }

            Console.ReadKey();
        }

        private void ExportComprehensiveReportToFolder(string folderPath)
        {
            try
            {
                string reportPath = Path.Combine(folderPath, "BaoCaoTongHop.txt");

                using (StreamWriter writer = new StreamWriter(reportPath, false, Encoding.UTF8))
                {
                    writer.WriteLine("BÁO CÁO TỔNG HỢP HỆ THỐNG NHÀ HÀNG");
                    writer.WriteLine($"Thời gian xuất: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                    writer.WriteLine("==========================================");
                    writer.WriteLine();

                    writer.WriteLine($"Tổng số món ăn: {dishes.Count}");
                    writer.WriteLine($"Tổng số nguyên liệu: {ingredients.Count}");
                    writer.WriteLine($"Tổng số combo: {combos.Count}");
                    writer.WriteLine($"Tổng số đơn hàng: {orders.Count}");
                    writer.WriteLine($"Tổng số người dùng: {users.Count}");
                    writer.WriteLine();

                    var completedOrders = orders.Values.Where(o => o.Status == OrderStatus.Completed).ToList();
                    writer.WriteLine($"Tổng doanh thu: {completedOrders.Sum(o => o.TotalAmount):N0}đ");
                    writer.WriteLine($"Số đơn hoàn thành: {completedOrders.Count}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi xuất báo cáo: {ex.Message}");
            }
        }

        // ==================== UNDO/REDO MENU ====================
        private void ShowUndoRedoMenu()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("UNDO/REDO - HOÀN TÁC/LÀM LẠI");
                Console.WriteLine("==============================");
                Console.WriteLine("(Nhập X để thoát)");

                Console.WriteLine($"🔄 Có thể Undo: {(undoRedoService.CanUndo ? "Có" : "Không")}");
                Console.WriteLine($"🔁 Có thể Redo: {(undoRedoService.CanRedo ? "Có" : "Không")}");

                Console.WriteLine("\nHƯỚNG DẪN SỬ DỤNG:");
                Console.WriteLine("• Undo (Hoàn tác): Quay lại thao tác trước đó");
                Console.WriteLine("• Redo (Làm lại): Thực hiện lại thao tác vừa hoàn tác");
                Console.WriteLine("• Hỗ trợ: Thêm món, Cập nhật món, Xóa món (đơn lẻ và hàng loạt)");
                Console.WriteLine("• Lưu ý: Chỉ hoàn tác được các thao tác trong phiên làm việc hiện tại");

                Console.WriteLine("\n1. Undo (Hoàn tác)");
                Console.WriteLine("2. Redo (Làm lại)");
                Console.WriteLine("3. Xem lịch sử thao tác có thể hoàn tác");
                Console.WriteLine("0. Quay lại");
                Console.Write("Chọn: ");

                string choice = Console.ReadLine();
                if (choice.ToLower() == "x") return;

                switch (choice)
                {
                    case "1":
                        if (undoRedoService.CanUndo)
                        {
                            undoRedoService.Undo();
                            SaveAllData();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("✅ Đã thực hiện Undo thành công!");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("⚠️ Không thể thực hiện Undo!");
                            Console.ResetColor();
                        }
                        break;
                    case "2":
                        if (undoRedoService.CanRedo)
                        {
                            undoRedoService.Redo();
                            SaveAllData();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("✅ Đã thực hiện Redo thành công!");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("⚠️ Không thể thực hiện Redo!");
                            Console.ResetColor();
                        }
                        break;
                    case "3":
                        ShowUndoRedoHistory();
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("Lựa chọn không hợp lệ!");
                        break;
                }

                Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
            }
        }

        private void ShowUndoRedoHistory()
        {
            Console.WriteLine("\n📋 LỊCH SỬ THAO TÁC CÓ THỂ HOÀN TÁC:");
            Console.WriteLine("=====================================");

            // Lưu ý: Trong implementation thực tế, cần lưu thêm thông tin về command
            Console.WriteLine("• Thao tác gần nhất sẽ được hiển thị đầu tiên");
            Console.WriteLine("• Undo stack: " + (undoRedoService.CanUndo ? "Có thao tác" : "Trống"));
            Console.WriteLine("• Redo stack: " + (undoRedoService.CanRedo ? "Có thao tác" : "Trống"));
        }

        // ==================== EXPORT ALL DATA (ADMIN ONLY) ====================
        private void ExportAllSystemData()
        {
            if (currentUser.Role != UserRole.Admin)
            {
                ShowAccessDenied();
                return;
            }

            Console.Clear();
            Console.WriteLine("XUẤT TOÀN BỘ THÔNG TIN HỆ THỐNG");
            Console.WriteLine("================================");
            Console.WriteLine("(Chỉ dành cho Quản trị viên)");
            Console.WriteLine("(Nhập X để hủy)");

            try
            {
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOWNLOAD_FOLDER);
                string exportFolder = Path.Combine(downloadPath, $"System_Export_{DateTime.Now:yyyyMMddHHmmss}");

                if (!Directory.Exists(exportFolder))
                {
                    Directory.CreateDirectory(exportFolder);
                }

                Console.WriteLine($"\nĐang xuất dữ liệu đến: {exportFolder}");

                // Xuất danh sách người dùng
                ExportUsersToFile(exportFolder);

                // Xuất danh sách nguyên liệu
                ExportIngredientsToFile(exportFolder);

                // Xuất danh sách món ăn
                ExportDishesToFile(exportFolder);

                // Xuất danh sách combo
                ExportCombosToFile(exportFolder);

                // Xuất danh sách đơn hàng
                ExportOrdersToFile(exportFolder);

                // Xuất lịch sử thao tác
                ExportAuditLogsToFile(exportFolder);

                // Xuất báo cáo tổng hợp
                ExportSystemSummary(exportFolder);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✅ Xuất toàn bộ dữ liệu thành công!");
                Console.WriteLine($"📁 Đường dẫn: {exportFolder}");
                Console.ResetColor();

                auditLogs.Add(new AuditLog(currentUser.Username, "EXPORT_ALL_SYSTEM_DATA", "SYSTEM", "", "Xuất toàn bộ thông tin hệ thống"));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Lỗi khi xuất dữ liệu: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }

        private void ExportUsersToFile(string folderPath)
        {
            string filePath = Path.Combine(folderPath, "01_DanhSachNguoiDung.txt");
            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                writer.WriteLine("DANH SÁCH NGƯỜI DÙNG");
                writer.WriteLine("=====================");
                writer.WriteLine($"Thời gian xuất: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                writer.WriteLine($"Tổng số: {users.Count} người dùng");
                writer.WriteLine();

                writer.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
                writer.WriteLine("║ {0,-15} {1,-25} {2,-12} {3,-15} {4,-10} ║",
                    "Tên đăng nhập", "Họ tên", "Vai trò", "Ngày tạo", "Trạng thái");
                writer.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

                foreach (var user in users.Values)
                {
                    writer.WriteLine("║ {0,-15} {1,-25} {2,-12} {3,-15} {4,-10} ║",
                        user.Username,
                        TruncateString(user.FullName, 25),
                        user.Role,
                        user.CreatedDate.ToString("dd/MM/yyyy"),
                        user.IsActive ? "Hoạt động" : "Vô hiệu hóa");
                }
                writer.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
            }
            Console.WriteLine($"📄 Đã xuất: {filePath}");
        }

        private void ExportIngredientsToFile(string folderPath)
        {
            string filePath = Path.Combine(folderPath, "02_DanhSachNguyenLieu.txt");
            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                writer.WriteLine("DANH SÁCH NGUYÊN LIỆU");
                writer.WriteLine("======================");
                writer.WriteLine($"Thời gian xuất: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                writer.WriteLine($"Tổng số: {ingredients.Count} nguyên liệu");
                writer.WriteLine();

                writer.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
                writer.WriteLine("║ {0,-8} {1,-25} {2,-10} {3,-8} {4,-10} {5,-12} ║",
                    "Mã", "Tên", "Đơn vị", "Số lượng", "Tối thiểu", "Giá/ĐV");
                writer.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

                foreach (var ing in ingredients.Values)
                {
                    string warning = ing.IsLowStock ? "⚠️" : "";
                    writer.WriteLine("║ {0,-8} {1,-25} {2,-10} {3,-8} {4,-10} {5,-12} {6,-2} ║",
                        ing.Id,
                        TruncateString(ing.Name, 25),
                        ing.Unit,
                        ing.Quantity,
                        ing.MinQuantity,
                        $"{ing.PricePerUnit:N0}đ",
                        warning);
                }
                writer.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");

                // Thống kê nguyên liệu sắp hết
                var lowStock = ingredients.Values.Where(ing => ing.IsLowStock).ToList();
                if (lowStock.Any())
                {
                    writer.WriteLine($"\n⚠️  CẢNH BÁO: Có {lowStock.Count} nguyên liệu sắp hết:");
                    foreach (var ing in lowStock)
                    {
                        writer.WriteLine($"- {ing.Name}: {ing.Quantity} {ing.Unit} (Tối thiểu: {ing.MinQuantity} {ing.Unit})");
                    }
                }
            }
            Console.WriteLine($"📄 Đã xuất: {filePath}");
        }

        private void ExportDishesToFile(string folderPath)
        {
            string filePath = Path.Combine(folderPath, "03_DanhSachMonAn.txt");
            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                writer.WriteLine("DANH SÁCH MÓN ĂN");
                writer.WriteLine("================");
                writer.WriteLine($"Thời gian xuất: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                writer.WriteLine($"Tổng số: {dishes.Count} món ăn");
                writer.WriteLine();

                writer.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
                writer.WriteLine("║ {0,-8} {1,-25} {2,-15} {3,-12} {4,-10} {5,-8} ║",
                    "Mã", "Tên món", "Nhóm", "Giá", "Tình trạng", "Đã bán");
                writer.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

                foreach (var dish in dishes.Values)
                {
                    string status = dish.IsAvailable ? "Có sẵn" : "Hết hàng";
                    writer.WriteLine("║ {0,-8} {1,-25} {2,-15} {3,-12} {4,-10} {5,-8} ║",
                        dish.Id,
                        TruncateString(dish.Name, 25),
                        TruncateString(dish.Category, 15),
                        $"{dish.Price:N0}đ",
                        status,
                        dish.SalesCount);
                }
                writer.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");

                // Thống kê theo nhóm
                var categoryStats = dishes.Values.GroupBy(d => d.Category)
                    .Select(g => new { Category = g.Key, Count = g.Count(), TotalSales = g.Sum(d => d.SalesCount) })
                    .OrderByDescending(g => g.Count);

                writer.WriteLine($"\n📊 THỐNG KÊ THEO NHÓM MÓN:");
                foreach (var stat in categoryStats)
                {
                    writer.WriteLine($"- {stat.Category}: {stat.Count} món ({stat.TotalSales} lượt bán)");
                }
            }
            Console.WriteLine($"📄 Đã xuất: {filePath}");
        }

        private void ExportCombosToFile(string folderPath)
        {
            string filePath = Path.Combine(folderPath, "04_DanhSachCombo.txt");
            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                writer.WriteLine("DANH SÁCH COMBO");
                writer.WriteLine("================");
                writer.WriteLine($"Thời gian xuất: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                writer.WriteLine($"Tổng số: {combos.Count} combo");
                writer.WriteLine();

                writer.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
                writer.WriteLine("║ {0,-8} {1,-25} {2,-8} {3,-12} {4,-12} {5,-8} ║",
                    "Mã", "Tên combo", "Số món", "Giá gốc", "Giá KM", "Đã bán");
                writer.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

                foreach (var combo in combos.Values.Where(c => c.IsActive))
                {
                    combo.CalculateOriginalPrice(dishes);
                    writer.WriteLine("║ {0,-8} {1,-25} {2,-8} {3,-12} {4,-12} {5,-8} ║",
                        combo.Id,
                        TruncateString(combo.Name, 25),
                        combo.DishIds.Count,
                        $"{combo.OriginalPrice:N0}đ",
                        $"{combo.FinalPrice:N0}đ",
                        combo.SalesCount);
                }
                writer.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
            }
            Console.WriteLine($"📄 Đã xuất: {filePath}");
        }

        private void ExportOrdersToFile(string folderPath)
        {
            string filePath = Path.Combine(folderPath, "05_DanhSachDonHang.txt");
            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                writer.WriteLine("DANH SÁCH ĐƠN HÀNG");
                writer.WriteLine("===================");
                writer.WriteLine($"Thời gian xuất: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                writer.WriteLine($"Tổng số: {orders.Count} đơn hàng");
                writer.WriteLine();

                writer.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
                writer.WriteLine("║ {0,-15} {1,-20} {2,-12} {3,-15} {4,-12} ║",
                    "Mã đơn", "Khách hàng", "Số món", "Tổng tiền", "Trạng thái");
                writer.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

                foreach (var order in orders.Values.OrderByDescending(o => o.OrderDate))
                {
                    writer.WriteLine("║ {0,-15} {1,-20} {2,-12} {3,-15} {4,-12} ║",
                        order.Id,
                        TruncateString(order.CustomerName, 20),
                        order.Items.Count,
                        $"{order.TotalAmount:N0}đ",
                        GetStatusText(order.Status));
                }
                writer.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");

                // Thống kê trạng thái
                var statusStats = orders.Values.GroupBy(o => o.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count(), TotalAmount = g.Sum(o => o.TotalAmount) });

                writer.WriteLine($"\n📊 THỐNG KÊ TRẠNG THÁI ĐƠN HÀNG:");
                foreach (var stat in statusStats)
                {
                    writer.WriteLine($"- {GetStatusText(stat.Status)}: {stat.Count} đơn ({stat.TotalAmount:N0}đ)");
                }
            }
            Console.WriteLine($"📄 Đã xuất: {filePath}");
        }

        private void ExportAuditLogsToFile(string folderPath)
        {
            string filePath = Path.Combine(folderPath, "06_LichSuThaoTac.txt");
            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                writer.WriteLine("LỊCH SỬ THAO TÁC");
                writer.WriteLine("================");
                writer.WriteLine($"Thời gian xuất: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                writer.WriteLine($"Tổng số: {auditLogs.Count} bản ghi");
                writer.WriteLine();

                writer.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
                writer.WriteLine("║ {0,-16} {1,-12} {2,-10} {3,-15} {4,-20} ║",
                    "Thời gian", "Người dùng", "Thao tác", "Thực thể", "Chi tiết");
                writer.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

                foreach (var log in auditLogs.OrderByDescending(a => a.Timestamp).Take(100)) // Giới hạn 100 bản ghi gần nhất
                {
                    writer.WriteLine("║ {0,-16} {1,-12} {2,-10} {3,-15} {4,-20} ║",
                        log.Timestamp.ToString("dd/MM HH:mm"),
                        log.Username,
                        log.Action,
                        $"{log.EntityType}:{log.EntityId}",
                        TruncateString(log.Details, 20));
                }
                writer.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
            }
            Console.WriteLine($"📄 Đã xuất: {filePath}");
        }

        private void ExportSystemSummary(string folderPath)
        {
            string filePath = Path.Combine(folderPath, "00_TongQuanHeThong.txt");
            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                writer.WriteLine("TỔNG QUAN HỆ THỐNG NHÀ HÀNG");
                writer.WriteLine("=============================");
                writer.WriteLine($"Thời gian xuất: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                writer.WriteLine($"Người xuất: {currentUser.FullName} ({currentUser.Username})");
                writer.WriteLine();

                // Thống kê tổng quan
                writer.WriteLine("📊 THỐNG KÊ TỔNG QUAN:");
                writer.WriteLine($"• Người dùng: {users.Count}");
                writer.WriteLine($"• Nguyên liệu: {ingredients.Count}");
                writer.WriteLine($"• Món ăn: {dishes.Count}");
                writer.WriteLine($"• Combo: {combos.Count}");
                writer.WriteLine($"• Đơn hàng: {orders.Count}");
                writer.WriteLine($"• Lịch sử thao tác: {auditLogs.Count}");

                // Thống kê doanh thu
                var completedOrders = orders.Values.Where(o => o.Status == OrderStatus.Completed).ToList();
                var totalRevenue = completedOrders.Sum(o => o.TotalAmount);
                writer.WriteLine($"\n💰 DOANH THU:");
                writer.WriteLine($"• Tổng doanh thu: {totalRevenue:N0}đ");
                writer.WriteLine($"• Số đơn hoàn thành: {completedOrders.Count}");
                writer.WriteLine($"• Đơn hàng trung bình: {(completedOrders.Any() ? totalRevenue / completedOrders.Count : 0):N0}đ");

                // Top món bán chạy
                var topDishes = dishes.Values.OrderByDescending(d => d.SalesCount).Take(5);
                writer.WriteLine($"\n🏆 TOP 5 MÓN BÁN CHẠY:");
                foreach (var dish in topDishes)
                {
                    writer.WriteLine($"• {dish.Name}: {dish.SalesCount} lượt - {dish.Price * dish.SalesCount:N0}đ");
                }

                // Cảnh báo
                var lowStockIngredients = ingredients.Values.Where(ing => ing.IsLowStock).ToList();
                if (lowStockIngredients.Any())
                {
                    writer.WriteLine($"\n⚠️  CẢNH BÁO TỒN KHO:");
                    writer.WriteLine($"• Có {lowStockIngredients.Count} nguyên liệu sắp hết");
                }

                writer.WriteLine($"\n📁 Thư mục xuất dữ liệu: {folderPath}");
            }
            Console.WriteLine($"📄 Đã xuất: {filePath}");
        }


        // ==================== PASSWORD CHANGE ====================
        private void ChangePassword()
        {
            Console.Clear();
            Console.WriteLine("ĐỔI MẬT KHẨU");
            Console.WriteLine("==============");

            Console.Write("Mật khẩu hiện tại: ");
            string currentPassword = ReadPassword();

            if (!SecurityService.VerifyPassword(currentPassword, currentUser.PasswordHash))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n❌ Mật khẩu hiện tại không đúng!");
                Console.ResetColor();
                Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
                return;
            }

            Console.Write("\nMật khẩu mới: ");
            string newPassword = ReadPassword();

            Console.Write("\nXác nhận mật khẩu mới: ");
            string confirmPassword = ReadPassword();

            if (newPassword != confirmPassword)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n❌ Mật khẩu xác nhận không khớp!");
                Console.ResetColor();
                Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
                return;
            }

            if (newPassword.Length < 6)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n❌ Mật khẩu phải có ít nhất 6 ký tự!");
                Console.ResetColor();
                Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
                Console.ReadKey();
                return;
            }

            currentUser.PasswordHash = SecurityService.HashPassword(newPassword);
            users[currentUser.Username] = currentUser;
            auditLogs.Add(new AuditLog(currentUser.Username, "CHANGE_PASSWORD", "USER", currentUser.Username));
            SaveAllData();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n✅ Đổi mật khẩu thành công!");
            Console.ResetColor();
            Console.WriteLine("Nhấn phím bất kỳ để tiếp tục...");
            Console.ReadKey();
        }
    }
    // ==================== DATA REPOSITORY WRAPPER ====================

    public class DataRepository
    {
        private Dictionary<string, User> users;
        private Dictionary<string, Ingredient> ingredients;
        private Dictionary<string, Dish> dishes;
        private Dictionary<string, Combo> combos;
        private Dictionary<string, Order> orders;
        private List<AuditLog> auditLogs;

        public DataRepository(
            Dictionary<string, User> users,
            Dictionary<string, Ingredient> ingredients,
            Dictionary<string, Dish> dishes,
            Dictionary<string, Combo> combos,
            Dictionary<string, Order> orders,
            List<AuditLog> auditLogs)
        {
            this.users = users;
            this.ingredients = ingredients;
            this.dishes = dishes;
            this.combos = combos;
            this.orders = orders;
            this.auditLogs = auditLogs;
        }

        public Dictionary<string, User> Users { get { return users; } set { users = value; } }
        public Dictionary<string, Ingredient> Ingredients { get { return ingredients; } set { ingredients = value; } }
        public Dictionary<string, Dish> Dishes { get { return dishes; } set { dishes = value; } }
        public Dictionary<string, Combo> Combos { get { return combos; } set { combos = value; } }
        public Dictionary<string, Order> Orders { get { return orders; } set { orders = value; } }
        public List<AuditLog> AuditLogs { get { return auditLogs; } set { auditLogs = value; } }
    }

    // ==================== MAIN PROGRAM ====================
    class Program
    {
        static void Main(string[] args)
        {
            RestaurantSystem system = new RestaurantSystem();
            system.Run();
        }
    }
}