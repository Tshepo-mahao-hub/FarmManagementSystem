// Program.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Interfaces
public interface IAccount
{
    bool Login(string username, string password);
}

public interface IManagement
{
    void AddAnimal(Animal animal);
    bool RemoveAnimal(int id);
    List<Animal> ListAnimals();
    bool UpdateAnimal(int id, Action<Animal> updateAction);
}

// Abstract Person
public abstract class Person : IAccount
{
    public string Name { get; protected set; }
    public int Age { get; protected set; }
    public string ID { get; protected set; }

    public Person(string name, int age, string id)
    {
        Name = name;
        Age = age;
        ID = id;
    }

    public abstract bool Login(string username, string password);
}

// Admin - can manage animals
public class Admin : Person, IManagement
{
    private readonly FarmRepository _repo;

    // Admin credentials hard-coded per requirements (could be loaded from config in real apps)
    private const string AdminUsername = "admin";
    private const string AdminPassword = "admin123";

    public Admin(string name, int age, string id, FarmRepository repo) : base(name, age, id)
    {
        _repo = repo;
    }

    public override bool Login(string username, string password)
    {
        return username == AdminUsername && password == AdminPassword;
    }

    public void AddAnimal(Animal animal)
    {
        _repo.Add(animal);
    }

    public bool RemoveAnimal(int id)
    {
        return _repo.Remove(id);
    }

    public List<Animal> ListAnimals()
    {
        return _repo.GetAll();
    }

    public bool UpdateAnimal(int id, Action<Animal> updateAction)
    {
        return _repo.Update(id, updateAction);
    }
}

// Farmer - can only view animals
public class Farmer : Person
{
    private const string FarmerUsername = "farmer1";
    private const string FarmerPassword = "farmer123";

    public Farmer(string name, int age, string id) : base(name, age, id) { }

    public override bool Login(string username, string password)
    {
        return username == FarmerUsername && password == FarmerPassword;
    }
}

// Animal class
public class Animal
{
    // Use an integer ID for simplicity
    public int ID { get; set; }
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string Species { get; set; } = "";

    public override string ToString()
    {
        return $"ID: {ID}, Name: {Name}, Age: {Age}, Species: {Species}";
    }

    // CSV serialization helper
    public string ToCsv()
    {
        // Escape commas by wrapping fields with quotes if necessary
        string safe(string s) => s.Contains(",") ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        return $"{ID},{safe(Name)},{Age},{safe(Species)}";
    }

    public static Animal FromCsv(string csvLine)
    {
        // Very simple parser to handle quoted values
        // We'll split by commas but respect quoted fields
        var parts = new List<string>();
        bool inQuotes = false;
        var current = "";
        for (int i = 0; i < csvLine.Length; i++)
        {
            char c = csvLine[i];
            if (c == '"' )
            {
                // toggle inQuotes or handle escaped quote
                if (inQuotes && i+1 < csvLine.Length && csvLine[i+1] == '"')
                {
                    current += '"';
                    i++; // skip next quote
                }
                else inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                parts.Add(current);
                current = "";
            }
            else current += c;
        }
        parts.Add(current);

        if (parts.Count < 4) throw new FormatException("Invalid CSV format for Animal.");

        return new Animal
        {
            ID = int.Parse(parts[0]),
            Name = parts[1],
            Age = int.Parse(parts[2]),
            Species = parts[3]
        };
    }
}

// Custom exception for invalid login
public class InvalidLoginException : Exception
{
    public InvalidLoginException(string message) : base(message) { }
}

// Repository that persists animals to a text file
public class FarmRepository
{
    private readonly string _filePath;
    private readonly List<Animal> _animals;
    private int _nextId;

    public FarmRepository(string filePath)
    {
        _filePath = filePath;
        _animals = new List<Animal>();
        LoadFromFile();
    }

    private void LoadFromFile()
    {
        _animals.Clear();
        _nextId = 1;

        if (!File.Exists(_filePath))
        {
            // create empty file
            using (File.Create(_filePath)) { }
            return;
        }

        var lines = File.ReadAllLines(_filePath);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var a = Animal.FromCsv(line);
                _animals.Add(a);
                if (a.ID >= _nextId) _nextId = a.ID + 1;
            }
            catch (Exception)
            {
                // Skip invalid lines but continue loading others
                continue;
            }
        }
    }

    private void SaveToFile()
    {
        var lines = _animals.Select(a => a.ToCsv()).ToArray();
        File.WriteAllLines(_filePath, lines);
    }

    public List<Animal> GetAll()
    {
        // return a copy to prevent external modification
        return _animals.Select(a => new Animal { ID = a.ID, Name = a.Name, Age = a.Age, Species = a.Species }).ToList();
    }

    public void Add(Animal a)
    {
        a.ID = _nextId++;
        _animals.Add(a);
        SaveToFile();
    }

    public bool Remove(int id)
    {
        var item = _animals.FirstOrDefault(x => x.ID == id);
        if (item == null) return false;
        _animals.Remove(item);
        SaveToFile();
        return true;
    }

    public bool Update(int id, Action<Animal> updateAction)
    {
        var item = _animals.FirstOrDefault(x => x.ID == id);
        if (item == null) return false;
        updateAction(item);
        SaveToFile();
        return true;
    }

    public Animal? GetById(int id)
    {
        var item = _animals.FirstOrDefault(x => x.ID == id);
        if (item == null) return null;
        return new Animal { ID = item.ID, Name = item.Name, Age = item.Age, Species = item.Species };
    }
}

// Main program
public class Program
{
    private static FarmRepository repo = new FarmRepository("animals.txt");

    public static void Main()
    {
        Console.WriteLine("=== Farm Management System ===");

        try
        {
            var person = LoginScreen();

            if (person is Admin admin)
            {
                AdminMenu(admin);
            }
            else if (person is Farmer farmer)
            {
                FarmerMenu(farmer);
            }
            else
            {
                Console.WriteLine("Unrecognized user type. Exiting.");
            }
        }
        catch (InvalidLoginException ex)
        {
            Console.WriteLine($"Login failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }

        Console.WriteLine("Exiting application. Goodbye.");
    }

    private static Person LoginScreen()
    {
        Console.Write("Enter username: ");
        string username = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Enter password: ");
        string password = ReadPasswordMasked();

        // Create repo-backed admin and farmer (Farmer doesn't need repo)
        var admin = new Admin("Administrator", 30, "A1", repo);
        var farmer = new Farmer("Farmer John", 40, "F1");

        bool adminOk = false;
        bool farmerOk = false;
        try { adminOk = admin.Login(username, password); } catch { adminOk = false; }
        try { farmerOk = farmer.Login(username, password); } catch { farmerOk = false; }

        if (adminOk) return admin;
        if (farmerOk) return farmer;

        throw new InvalidLoginException("Invalid username or password.");
    }

    private static void AdminMenu(Admin admin)
    {
        while (true)
        {
            Console.WriteLine("\n--- Admin Menu ---");
            Console.WriteLine("1. Add animal");
            Console.WriteLine("2. Remove animal");
            Console.WriteLine("3. List animals");
            Console.WriteLine("4. Update animal");
            Console.WriteLine("5. Logout");
            Console.Write("Choice: ");
            var choice = Console.ReadLine()?.Trim();

            try
            {
                switch (choice)
                {
                    case "1":
                        DoAddAnimal(admin); break;
                    case "2":
                        DoRemoveAnimal(admin); break;
                    case "3":
                        DoListAnimals(admin.ListAnimals()); break;
                    case "4":
                        DoUpdateAnimal(admin); break;
                    case "5":
                        return;
                    default:
                        Console.WriteLine("Invalid choice.");
                        break;
                }
            }
            catch (FormatException)
            {
                Console.WriteLine("Invalid number format. Please enter a valid integer.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    private static void FarmerMenu(Farmer farmer)
    {
        while (true)
        {
            Console.WriteLine("\n--- Farmer Menu ---");
            Console.WriteLine("1. View animals");
            Console.WriteLine("2. View animal details by ID");
            Console.WriteLine("3. Logout");
            Console.Write("Choice: ");
            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    DoListAnimals(repo.GetAll());
                    break;
                case "2":
                    Console.Write("Enter animal ID: ");
                    if (!int.TryParse(Console.ReadLine(), out int id))
                    {
                        Console.WriteLine("Invalid ID format. Must be integer.");
                        break;
                    }
                    var a = repo.GetById(id);
                    if (a == null)
                        Console.WriteLine($"No animal found with ID {id}.");
                    else
                        Console.WriteLine(a);
                    break;
                case "3":
                    return;
                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }
        }
    }

    private static void DoAddAnimal(Admin admin)
    {
        Console.WriteLine("Adding new animal. Fill details:");

        string name;
        while (true)
        {
            Console.Write("Name: ");
            name = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                Console.WriteLine("Name cannot be empty.");
                continue;
            }
            break;
        }

        int age;
        while (true)
        {
            Console.Write("Age (positive integer): ");
            var input = Console.ReadLine()?.Trim();
            if (!int.TryParse(input, out age) || age < 0)
            {
                Console.WriteLine("Invalid age. Enter a positive integer (0 or greater).");
                continue;
            }
            break;
        }

        string species;
        while (true)
        {
            Console.Write("Species: ");
            species = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(species))
            {
                Console.WriteLine("Species cannot be empty.");
                continue;
            }
            break;
        }

        var animal = new Animal { Name = name, Age = age, Species = species };
        admin.AddAnimal(animal);
        Console.WriteLine($"Added animal with ID {animal.ID}.");
    }

    private static void DoRemoveAnimal(Admin admin)
    {
        Console.Write("Enter ID of animal to remove: ");
        var input = Console.ReadLine()?.Trim();
        if (!int.TryParse(input, out int id))
        {
            Console.WriteLine("Invalid ID format. Must be integer.");
            return;
        }

        bool removed = admin.RemoveAnimal(id);
        if (removed) Console.WriteLine($"Animal {id} removed.");
        else Console.WriteLine($"No animal found with ID {id}.");
    }

    private static void DoListAnimals(List<Animal> list)
    {
        Console.WriteLine("\n--- Animals ---");
        if (list.Count == 0)
        {
            Console.WriteLine("No animals found.");
            return;
        }

        foreach (var a in list)
        {
            Console.WriteLine(a.ToString());
        }
    }

    private static void DoUpdateAnimal(Admin admin)
    {
        Console.Write("Enter ID of animal to update: ");
        var input = Console.ReadLine()?.Trim();
        if (!int.TryParse(input, out int id))
        {
            Console.WriteLine("Invalid ID format.");
            return;
        }

        var exists = repo.GetById(id);
        if (exists == null)
        {
            Console.WriteLine($"No animal found with ID {id}.");
            return;
        }

        Console.WriteLine("Leave field blank to keep current value.");
        Console.WriteLine($"Current: {exists}");

        Console.Write("New name: ");
        var newName = Console.ReadLine()?.Trim();
        Console.Write("New age: ");
        var newAgeStr = Console.ReadLine()?.Trim();
        Console.Write("New species: ");
        var newSpecies = Console.ReadLine()?.Trim();

        bool ok = admin.UpdateAnimal(id, a =>
        {
            if (!string.IsNullOrWhiteSpace(newName)) a.Name = newName;
            if (!string.IsNullOrWhiteSpace(newAgeStr) && int.TryParse(newAgeStr, out int newAge) && newAge >= 0) a.Age = newAge;
            if (!string.IsNullOrWhiteSpace(newSpecies)) a.Species = newSpecies;
        });

        if (ok) Console.WriteLine("Animal updated.");
        else Console.WriteLine("Failed to update animal.");
    }

    // Helper to read password without echoing
    private static string ReadPasswordMasked()
    {
        var pass = "";
        ConsoleKeyInfo key;
        while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace)
            {
                if (pass.Length > 0)
                {
                    pass = pass.Substring(0, pass.Length - 1);
                    Console.Write("\b \b");
                }
            }
            else
            {
                pass += key.KeyChar;
                Console.Write("*");
            }
        }
        Console.WriteLine();
        return pass;
    }
    
    }