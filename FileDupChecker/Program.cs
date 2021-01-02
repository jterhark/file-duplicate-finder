using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace FileDupChecker
{
    class Program
    {
        private static bool _verbose = false;
        private static bool _recursionEnabled = false;
        private static DirectoryInfo[] _locations = null;
        private static string[] _exclusions = null;
        private static Dictionary<string, List<string>> _hashes = new();
        private static int _numFiles = 0;
        private static int _numDirs = 0;
        private const int _resultsPadding = 20;

        static async void Main(string[] args)
        {
            var root = new RootCommand
            {
                new Option<bool>(
                    new[] {"--verbose", "-v"},
                    description: "Gives you all the tea",
                    getDefaultValue: () => false
                ),
                new Option<bool>(
                    new[] {"--recurse", "-r"},
                    description: "Find files recursively",
                    getDefaultValue: () => false
                ),
                new Option<DirectoryInfo[]>(
                    "--location",
                    description: "root folder to run this prog"),
                new Option<string>(
                    "--exclude",
                    description: "folders to exclude")
            };

            root.Description = "Find some file dups via some fancy md5 hashing";
            root.Handler = CommandHandler.Create<bool, bool, DirectoryInfo[], string[]>(HandleArgs);

            await root.InvokeAsync(args);

            JustDoIt();
        }

        //lazy string initialization to avoid building strings but keep verbose check here???
        private static void PrintVerbose(string msg)
        {
            if (_verbose) Console.WriteLine(msg);
        }

        private static void PrintLine(char c = '-', int length = 30)
        {
            Console.WriteLine(new string(c, length));
        }

        private static void HandleArgs(bool verbose, bool recurse, DirectoryInfo[] location, string[] exclude)
        {
            _verbose = verbose;
            _locations = location;
            _recursionEnabled = recurse;
            _exclusions = exclude;

            PrintVerbose("Spilling all the tea. (verbose)");
            PrintVerbose($"Recursion is {(recurse ? "enabled":"disabled" )}");

            //need at least one directory to work with
            if (_locations == null || _locations.Length == 0)
            {
                throw new ArgumentException("You must provide at least one location.");
            }

            //check if directories exist
            foreach (var loc in _locations)
            {
                if (!loc.Exists)
                {
                    Console.Error.WriteLine($"{loc} does not exist!");
                }

                PrintVerbose($"Accepted path: {loc}");
            }
        }

        private static void JustDoIt()
        {
            //calculate hashes for all files in specified directories
            foreach (var dir in _locations)
            {
                HandleDirectory(dir);
            }
            
            //find duplicate files
            var dups = _hashes
                .Where(x => x.Value.Count > 1)
                .ToList();
            
            
            //print results
            PrintLine();
            Console.WriteLine("RESULTS");
            PrintLine();
            Console.WriteLine($"{"Folders",-_resultsPadding}: {_numDirs}");
            Console.WriteLine($"{"Files",-_resultsPadding}: {_numFiles}");
            Console.WriteLine($"{"Dups Found",-_resultsPadding}: {dups.Count}");
            PrintLine();

            foreach (var dup in dups)
            {
                Console.WriteLine(dup.Key);
                Console.WriteLine();
                dup.Value.ForEach(Console.WriteLine);
                PrintLine();
            }
        }

        private static void HandleDirectory(DirectoryInfo dir)
        {
            //ignore excluded directories
            if(_exclusions?.Contains(dir.Name) ?? false){
                return;
            }

            using var md5 = MD5.Create();

            PrintVerbose($"Processing:  {dir.FullName}");
            
            //iterate over files in current directory
            foreach (var file in dir.EnumerateFiles())
            {
                using var stream = file.OpenRead();
                
                //compute hash
                var hashBytes = md5.ComputeHash(stream);
                var hash = BitConverter.ToString(hashBytes).Replace("-", "");
                
                //add to results
                if (_hashes.ContainsKey(hash))
                    _hashes[hash].Add(file.FullName);
                else
                    _hashes[hash] = new() {file.FullName};
                
                _numFiles++;
            }

            _numDirs++;

            if (!_recursionEnabled) return;
            
            //recurse through child directories
            foreach (var child in dir.EnumerateDirectories())
            {
                HandleDirectory(child);
            }
        }
    }
}