//
//  Program.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2017 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using Everlook.Explorer;
using Everlook.Silk;
using Everlook.UI;
using Everlook.Utility;
using FileTree.Tree.Serialized;
using GLib;
using log4net;
using log4net.Config;
using log4net.Repository.Hierarchy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Silk.NET.Core.Loader;
using Silk.NET.Core.Platform;

using Application = Gtk.Application;
using Task = System.Threading.Tasks.Task;

namespace Everlook
{
    /// <summary>
    /// The main entry class, containing the entry point and some top-level diagnostics.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// The entry point.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [STAThread]
        public static async Task Main(string[] args)
        {
            IconManager.LoadEmbeddedIcons();
            Application.Init();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Environment.SetEnvironmentVariable("GSETTINGS_SCHEMA_DIR", "share\\glib-2.0\\schemas\\");
            }

            const string configurationName = "Everlook.log4net.config";
            var logConfig = new XmlDocument();
            await using (var configStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(configurationName))
            {
                if (configStream is null)
                {
                    throw new InvalidOperationException("The log4net configuration stream could not be found.");
                }

                logConfig.Load(configStream);
            }

            var repo = LogManager.CreateRepository(Assembly.GetEntryAssembly(), typeof(Hierarchy));
            XmlConfigurator.Configure(repo, logConfig["log4net"]);

            SilkManager.Register<GLSymbolLoader>(new GDKGLSymbolLoader());

            var nodeType = (GType)typeof(SerializedNode);
            GType.Register(nodeType, typeof(SerializedNode));

            var referenceType = (GType)typeof(FileReference);
            GType.Register(referenceType, typeof(FileReference));

            var host = CreateHostBuilder(args).Build();

            var app = host.Services.GetRequiredService<Startup>();
            app.Start();

            Application.Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) => new HostBuilder()
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "config"));
                config.AddJsonFile("appsettings.json");
            })
            .ConfigureServices((hostingContext, services) =>
            {
                services.AddSingleton(new Application("net.Everlook.Everlook", ApplicationFlags.None));

                services.AddTransient(MainWindow.Create);
                services.AddSingleton<Startup>();
            })
            .ConfigureLogging(l =>
            {
                l.ClearProviders();
                l.AddLog4Net();
            });
    }
}
