//  GFAX - Gnome fax application
//  Copyright (C) 2003 - 2008 George A. Farris
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Library General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA 02111-1307, USA.

namespace gfax {
	using GLib;
	using Gtk;
	using GtkSharp;
	using Mono.Unix;
	using System;
	using System.IO;
	using System.Collections;
	using System.Text;			//Encoding.ASCII.GetBytes
	using System.Threading;
	using System.Diagnostics;


	
	public class gfax
	{	
		// Gfax Global variables ( ya, ya I know)
		//
		// list of contacts of type Contact to xmit, we use this everywhere so it is
		// simpler to make it public and global.
		public static ArrayList Destinations = new ArrayList();
		public static DateTime timeToSend;
		public static bool quitAfterSend = false;
		public static bool fromSendWizard = false;
		public static bool sendWizardResolution = false;
		public static bool sendWizardEmailNotify = false;
		public static string sendWizardEmailAddress = null;
		public static string hylafaxPassword = null;
		public static string ConfigDirectory = null;
		public static string SpoolDirectory = null;
		public static bool activeNetwork = false;
		public static Gnome.AppBar GAppbar = null;
		public static Gtk.ProgressBar MainProgressBar = null;
		public static Gtk.TextBuffer GStatusTextBuffer = null;
		public static Gtk.Window MainWindow = null;
		public static string asyncFolderMessageBuffer = "";
		public static string asyncServerMessageBuffer = "";
		public static string asyncReadType = "";
		
		public static Efax efax;
		public static Status Status;
		public static Pulser Pulser;
		
		public static string filename = null;
		public static string[] pargs;
		
		const bool TOPLEVEL = true;
				
				
		public static void Main (string[] args)
		{
		
			string HOMEDIR = Environment.GetEnvironmentVariable("HOME");
			string USER = Environment.GetEnvironmentVariable("USER");
			ConfigDirectory = HOMEDIR + "/.config/gfax";
			SpoolDirectory = HOMEDIR + "/.local/share/gfax/spool";
			pargs = args;
			
			// TODO put this is try/catch
			if ( !Directory.Exists(ConfigDirectory)) {
				if ( !Directory.Exists(HOMEDIR + "/.config")) {
					Directory.CreateDirectory(HOMEDIR + "/.config");
				}
				// Move data from old ~/.etc directory
				if ( Directory.Exists(HOMEDIR + "/.etc/gfax")) {
					Directory.Move(HOMEDIR + "/.etc/gfax", ConfigDirectory);
				} else {
					Directory.CreateDirectory(ConfigDirectory);
				}
			}
			if ( !Directory.Exists(HOMEDIR + "/.local/share/gfax/spool") ) {
				if ( !Directory.Exists(HOMEDIR + "/.local/share/gfax") ) {
					if ( !Directory.Exists(HOMEDIR + "/.local/share") ) {
						if ( !Directory.Exists(HOMEDIR + "/.local") )			
							Directory.CreateDirectory(HOMEDIR + "/.local");
						Directory.CreateDirectory(HOMEDIR + "/.local/share");
					}
					Directory.CreateDirectory(HOMEDIR + "/.local/share/gfax");
				}
				Directory.CreateDirectory(HOMEDIR + "/.local/share/gfax/spool");
				Directory.CreateDirectory(HOMEDIR + "/.local/share/gfax/spool/doneq");
				Directory.CreateDirectory(HOMEDIR + "/.local/share/gfax/spool/recq");
			}
			
			// Clean out the spool/tif directory - incoming faxes
			if ( !Directory.Exists(HOMEDIR + "/.local/share/gfax/spool/tif")) {
				Directory.CreateDirectory(HOMEDIR + "/.local/share/gfax/spool/tif");
			} else {
				Directory.Delete(HOMEDIR + "/.local/share/gfax/spool/tif/", true);
				Directory.CreateDirectory(HOMEDIR + "/.local/share/gfax/spool/tif");
			}
			
			
			
			// Initialize GETTEXT
			Catalog.Init ("gfax", Defines.GNOME_LOCALE_DIR);

			// handle command line args ourselves					
			for (int i=0; i < args.Length; i++ ) {
				//Console.WriteLine("{0} {1}", i, args[i]);
				switch (args[i])
				{
					case "--help" :
								Console.WriteLine (Catalog.GetString("Gfax help..."));
								Console.WriteLine ("Gfax spool dir -> {0}", SpoolDirectory);
								break;
					case "-f" :	// file name is present
								filename = args[i+1];
								break;
					//case "-q" :	// immediately quit after sending fax
					//			filename = args[i+1];
					//			break;
					default:
								if (File.Exists(args[i]))
									filename = args[i];
								break;
				}
			}
			
			try {
				if ( Settings.RunSetupAtStart ) {
					// Set some default preferences.
					Settings.TransmitAgent = "efax";
					Settings.SendNow = true;
					Settings.EfaxModemDevice = "ttyS0";
					Settings.RefreshQueueInterval = 15;
					Settings.RefreshQueueEnabled = true;
				}
			} catch (Exception e) { 
				//TODO  HIG love required
				G_Message gm = new G_Message(
					Catalog.GetString(
@"Gconfd cannot find your settings. 
If you are running Gfax immediately 
after an installation, you may have 
to log out and log back in again."), TOPLEVEL);
				Console.WriteLine("Exception in main.cs {0}", e);
				Environment.Exit(0);
			}

			// If we have a file name run the send dialog
			if (filename != null) {
				GfaxSend sd = new GfaxSend (filename, args);
				FileInfo f = new FileInfo(filename);
				
				// send the faxes
				if (sd.DoSend) {
					fromSendWizard = true;
					
					// Start the fax daemon if efax
					if (Settings.TransmitAgent == "efax") {
						efax = new Efax();
						efax.run_efaxd();
					}

					Fax.sendfax(filename);
					// delete the spool file (~.local/share/gfax/spool/D.*)
					if (File.Exists(String.Concat(SpoolDirectory, "/", f.Name)))
						File.Delete(String.Concat(SpoolDirectory, "/", f.Name));
						
					//if (!quitAfterSend) {
						//Gfax gf = new Gfax (filename, args);
					//}
				}
				
				// delete the spool file that gfax created if it exists
				if (File.Exists(String.Concat("/var/spool/gfax/", USER, "/", f.Name)))
					File.Delete(String.Concat("/var/spool/gfax/", USER, "/",  f.Name));
				
			}else {


				// We need /var/spool/gfax/<user> to exsist and be 0777 perms
				//ACCESSPERMS = 0777
				if ( !Directory.Exists("/var/spool/gfax/" + USER)) {
					Directory.CreateDirectory("/var/spool/gfax/" + USER);
				}
				Mono.Unix.Native.Syscall.chmod("/var/spool/gfax/" + USER, Mono.Unix.Native.FilePermissions.ACCESSPERMS);
				
				FileSystemWatcher watcher = new FileSystemWatcher();
				watcher.Path = "/var/spool/gfax/" + USER;
			
				watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite 
        	   		| NotifyFilters.FileName | NotifyFilters.DirectoryName;
        		// Only watch text files.
        		watcher.Filter = "*";
				watcher.Created += new FileSystemEventHandler(TIOnChanged);
				// Begin watching.
        		watcher.EnableRaisingEvents = true;

				// Start the fax daemon if efax
				if (Settings.TransmitAgent == "efax") {
					efax = new Efax();
					efax.run_efaxd();
				}

				Gfax gf = new Gfax (filename, args);
			}
			
		} 
		
		// Define the event handlers.
    	private static void TIOnChanged(object source, FileSystemEventArgs e)
    	{
        	// Specify what is done when a file is changed, created, or deleted.
       		Console.WriteLine("File---: " +  e.FullPath + " " + e.ChangeType);
			//GfaxSend sd = new GfaxSend ("/home/george/log.txt", pargs);
    		
    		quitAfterSend = true;
    		
    		ProcessStartInfo pidInfo = new ProcessStartInfo();
			pidInfo.FileName = "gfax";

			pidInfo.Arguments = String.Concat("-f ",e.FullPath + " " + e.ChangeType);
			
			if (Settings.Faxtracing == true) {
				if (Settings.TransmitAgent == "efax") {
					Console.WriteLine("[Efax.send_init]\n  {0}", pidInfo.Arguments);
				}
			}
			
			System.Diagnostics.Process pid = System.Diagnostics.Process.Start(pidInfo);
			pid.WaitForExit();

		
		}

	}
}
