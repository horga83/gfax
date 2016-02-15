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

		// Sequence to send a file is:
		// 	1) Make sure we're connected
		//	2) Store the filename on the server [storefile_open]
		//	3) Send the file line by line
		//	4) Close the stream for sending the file
		//	5) For all the phone numbers do:
		//		a) job_new				[job_new]
		//		b) set all job parms	[job_parm_set]
		//		c) submit the job		[submit_job]
		//	6) Close the connection.

// Properties
// 	string Hostname, Username, Password
//	int IPPort

//#define DEBUGEFAX

namespace gfax {
	using Gtk;
	using Mono.Unix;
	using System;
	using System.IO;
	using System.Text;			//Encoding.ASCII.GetBytes
	using System.Collections;	
	using System.Net;
	using System.Threading;
	using System.Net.Sockets;
	using System.Diagnostics;


	public class Efax
	{	
		private static System.Threading.Thread thread;
		string user;
		
		
		//Speaker Volume
		string[] speakerVolume = {"L0","L1","L2","L3"};
		const int VERY_LOW = 0;
		const int LOW = 1;
		const int MEDIUM = 2;
		const int HIGH = 3;

		// Speaker modes
		string[] speakerMode = {"M0","M1","M2","M3"};
		const int NEVER = 0;
		const int UNTIL_CARRIER = 1;
		const int ALWAYS_ON = 2;
		const int ON_RECEIVE_ONLY = 3;
		
		//string[] modemType = {@"-j\Q4",@"-j\Q1",@"-j*F1",@"-j&H2&I0&R1&D3I4",@"-or"};
		
		
		/*
		# FCINIT='-j\Q4'                # AT&T (Dataport, Paradyne)
		# FCINIT='-j\Q1'                # Motorola (Power Modem, 3400 Pro,...)
		# FCINIT='-j*F1'                # QuickComm (Spirit II)
		# FCINIT='-j&H2&I0&R1&D3I4'     # USR (Courier, Sportster)
		# FCINIT='-or'                  # Multi-Tech (for bit reversal)
		*/
		
		// Don't need this anymore
		//TextWriter statusfile = null;
		
		public Efax () 
		{
		
			// Set initial modem settings, gconfsharp-schemagen doesn't like "&"
			// so can't set a default in gfax.schemas
			if ( Settings.EfaxModemInit == "" )
				Settings.EfaxModemInit = "-iZ -i&FE0&D2S7=120 -i&C0";
			if ( Settings.EfaxModemFcinit == "" )
				Settings.EfaxModemFcinit = "-j&H2&I0&R1&D3I4";
			if ( Settings.EfaxModemReset == "" )
				Settings.EfaxModemReset = "-kZ";
				
			//Don't need this anymore
			//statusfile = TextWriter.Synchronized(File.CreateText(gfax.Procfile));
		}
		
		public void close ()
		{
		}

		// Method status(queue)
		//
		//	queue is the queue fax system it can be one of:
		//	'sendq', 'doneq' or 'recvq'
		//
		// Return a string containing lines formatted like so
		// "jobid=number=status=owner=pages=dials=error=sendat\n"
		// such as:
		// "2=5551212=S=george=2=1=error message=2004/03/09 18.01.51\n"
		public string status (string queue)
		{
			StreamReader infile = null;
			string[] sts = new string[11];
			string path;
			string buf;
			
			
			if (queue == "sendq")
				path = gfax.SpoolDirectory;
			else if (queue == "doneq")
				path = gfax.SpoolDirectory + "/doneq";
			else 
				path = gfax.SpoolDirectory + "/recq";
				
			string[] control_files = Directory.GetFiles(path, "C_*");
			string[] lines = new string[control_files.Length];
			int num_lines = 0;
			
			foreach (string s in control_files) {
				try { 
					infile = File.OpenText(s);
										
					for (int i=0; (buf = infile.ReadLine()) != null; i++)  {
						string[] sa = buf.Split('=');
						if ( sa[1].Length != 0 )
							sts[i] = sa[1];
						else
							sts[i] = "-";
					}
					infile.Close();
					// Purge doneq files if older than 5 days.
					if ( queue == "doneq" )
						if ( (DateTime.Now).Subtract(File.GetCreationTime(s)).Days > 5 )
							File.Delete(s);

					lines[num_lines++] = String.Format("{0}={1}={2}={3}={4}={5}={6}={7}\n",
							sts[0],sts[2],sts[3],sts[4],sts[5],sts[6],sts[10],sts[7]);
				}
				catch (Exception e) { 
          Console.WriteLine("[Efax.status] Exception {0}", e);
					//return; 
				}
			}	
			
			return String.Concat(lines);
		}

		// send_init (string filename)
		//
		//	Here we should convert the file with ghostscript and return a directory 
		//  that points to the converted files to send, one per page.  There should 
		//  be some status messages passed to the user to let them know whats going on.   
		//  Maybe a dialog box.
		//
		//  We also setup the status message system.		
		public string send_init (string fname)
		{
			StreamReader fp = null;
			double lines = 0;
			string resolution = "204x98";  // normal res
			
			// Get new directory name and make it
			Random rand = new Random();
			string rand_file = rand.Next().ToString();
			string dir_name = String.Format("{0}/D_{1}",gfax.SpoolDirectory, rand_file);
			// TODO proper checks here, this is nasty
			Directory.CreateDirectory(dir_name);
				

			// get the fax options
			if (gfax.sendWizardResolution) {
					resolution = "204x196";
			}else {
				if (Settings.HiResolution)
					resolution = "204x196";
			}
			
			string papersize = Settings.EfaxPapersize;
			
			
			//figure out how many lines in the file for progress bar
			// TODO progress bar and error 
			try { fp = File.OpenText(fname); }
			catch (Exception e) {
        Console.WriteLine("[Efax.send_init] Exception: {0}", e);
      }
			
			while ( (fp.ReadLine()) != null ) {
				lines = lines + 1;
			}
			fp.Close();
			
			
			try { fp = File.OpenText(fname); }
			catch (Exception e) {
        Console.WriteLine("[Efax.send_init] Exception: {0}", e);
      }
			
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[Efax.send_init] File :{0} is open and has {1} lines", fname,lines);
			}
			
			fp.Close();
			
						
			// TODO need random temp file name
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[Efax.send_init] Converting file with gs");
				Console.WriteLine("  Filename   -> {0}",fname);
				Console.WriteLine("  Resolution -> {0}",resolution);
				Console.WriteLine("  Directory  -> {0}",dir_name);
				Console.WriteLine("  Papersize  -> {0}\n",papersize);
			}

			ProcessStartInfo pidInfo = new ProcessStartInfo();
			pidInfo.FileName = "gs";

			pidInfo.Arguments = String.Concat(
					"-q -sDEVICE=tiffg3 -r",
					resolution, 
					" -dNOPAUSE -dSAFER -dBATCH", 
					" -sOutputFile=",
					dir_name,
					"/tmp.%03d -sPAPERSIZE=",
					papersize, " ",fname);
			
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[Efax.send_init]\n  {0}", pidInfo.Arguments);
			}
			
			Process pid = Process.Start(pidInfo);
			pid.WaitForExit();
			
			return dir_name;
		}
		
		// Method send(string directory, contact)
		//
		// Sequence to send a file is:
		public void send (string directory, GfaxContact contact)
		{
			StreamWriter outfile;
			int pages;
			
			
			string emailAddress = Settings.EmailAddress;
			string emailNotify = "none";
			
			// Get advanced options
			if (gfax.fromSendWizard) {			
				if (gfax.sendWizardEmailNotify) {
					emailNotify = "done";
					emailAddress = gfax.sendWizardEmailAddress;
				}
			} else {
				if (Settings.EmailNotify)
					emailNotify = "done";
			}
			
			//2004/03/09 18.01.51
			// Format time to send
			DateTime st = DateTime.Now;
			string tts = String.Format("{0}/{1:00}/{2:00} {3:00}.{4:00}.00",
					st.Year, st.Month, st.Day, st.Hour, st.Minute);
						
			// get next jobid
			if ( Settings.EfaxNextJobid	> 998 )
				Settings.EfaxNextJobid = 1;
			else
				Settings.EfaxNextJobid++;
			
			// open directory and count files, that will be the number of pages.
			pages = Directory.GetFiles(directory).Length;
						
			// build filename
			Random rand = new Random();
			string rand_file = rand.Next().ToString();
			string tfname = String.Format("{0}/C_{1}.{2}",gfax.SpoolDirectory, contact.PhoneNumber, rand_file);
			
			// TODO error stuff
			//Open new status file
			try { outfile = File.CreateText(tfname); }
			catch (Exception e) { 
        Console.WriteLine("[Efax.send] Exception: {0}", e);
        return;
      }
			outfile.Write("Jobid=");
			outfile.WriteLine(Settings.EfaxNextJobid);
			outfile.Write("JobDirectory=");
			outfile.WriteLine(directory);
			outfile.Write("PhoneNumber=");
			outfile.WriteLine(contact.PhoneNumber);
			outfile.Write("Status=");
			outfile.WriteLine("P");
			outfile.Write("Owner=");
			outfile.WriteLine(Environment.GetEnvironmentVariable("USERNAME"));
			outfile.Write("Pages=");
			outfile.WriteLine(pages.ToString());
			outfile.Write("Dials=");
			outfile.WriteLine(0);
			outfile.Write("Sendat=");
			outfile.WriteLine(tts);
			outfile.Write("Notification=");
			outfile.WriteLine(emailNotify);
			outfile.Write("Email=");
			outfile.WriteLine(emailAddress);
			outfile.Write("ErrorMessage=");
			outfile.WriteLine("");
			outfile.Close();
			
			// jobid might need to be Convert.ToInt32

		}
		
		public void job_delete (string jobid)
		{	
			StreamReader infile = null;
			string[] sts = new string[11];
			string buf;
			
			string[] controlFiles = Directory.GetFiles(gfax.SpoolDirectory, "C_*");
						
			foreach (string controlFile in controlFiles) {
				try { 
					infile = File.OpenText(controlFile); 
						
					for (int i=0; (buf = infile.ReadLine()) != null; i++)  {
						string[] sa = buf.Split('=');
						if ( sa[1].Length != 0 )
							sts[i] = sa[1];
						else
							sts[i] = "-";
					}
					infile.Close();
					
					if ( sts[0] == jobid ) {
						File.Delete(controlFile);
						Directory.Delete(sts[1], true);
						break;
					}
				}
				catch (Exception e) {
          Console.WriteLine("[Efax.job_delete] Exception: {0}", e);
          continue;
        }
			}
			
			// end up here with control file name
			return;
		}

		public string job_kill (string jobid)
		{
			return null;
		}
		
		public void run_efaxd ()
		{
			// Create the thread object, passing in the efaxd method
      		WaitCallback callback = new WaitCallback(efaxd);
			ThreadPool.QueueUserWorkItem(callback);
		}
		
		/* efaxd
		 * For now just cycle through the control files and send the fax
		 *
		 */
		private void efaxd (object state)
		{
			thread = System.Threading.Thread.CurrentThread;
			
			StreamReader infile = null;
			string[] sts = new string[11];
			string buf;
			//bool fatalError = false;
			int retries;
			string speakerModeVolume;
			
			//string jobid, number, status, owner, pages, dials, error, sendat;
			System.Threading.Thread.Sleep(15000); // Don't start immediately
			
			while (true) {
				Application.Invoke (delegate {gfax.GAppbar.ClearStack();});
				Application.Invoke (delegate {gfax.GAppbar.Push(Catalog.GetString("Scanning control files."));});

				// while there are job files in the folder
				while (Directory.GetFiles(gfax.SpoolDirectory, "C_*").Length > 0) {
				
					string[] controlFiles = Directory.GetFiles(gfax.SpoolDirectory, "C_*");
					// for each job try send it					
					foreach (string controlFile in controlFiles) {
						if (Settings.Faxtracing == true) {
							Console.WriteLine("Control files are ----> {0}", controlFile);
						}
           
						//Application.Invoke (delegate {gfax.GAppbar.ClearStack();});
						Application.Invoke (delegate {gfax.GStatusTextBuffer.InsertAtCursor(
								Catalog.GetString("Running job " + controlFile));});
           
						try { 
							infile = File.OpenText(controlFile); 
							
							for (int i=0; (buf = infile.ReadLine()) != null; i++)  {
								string[] sa = buf.Split('=');
								if ( sa[1].Length != 0 )
									sts[i] = sa[1];
								else
									sts[i] = "-";
							}
							infile.Close();
							if (Settings.Faxtracing == true) {
								Console.WriteLine("[Efaxd] Buffer is {0}", buf);
							}
						}
						catch (Exception e) {
  	          Console.WriteLine("[Efax.efaxd] Exception: {0}", e);
						  continue;
					 }
						
						//lines[num_lines++] = String.Format("{0}={1}={2}={3}={4}={5}={6}={7}\n",
						//		sts[0],sts[2],sts[3],sts[4],sts[5],sts[6],sts[10],sts[7]);
           
						// If we aborted for some reason check the retries
						if (Convert.ToInt32(sts[6]) >= Settings.EfaxRetries) {
							sts[10] = Catalog.GetString("Busy retries exceeded");
							update_status_code(sts, controlFile, "F");
							continue;
						}
						
						sts[10] = "";	// clear error message
						update_status_code(sts, controlFile, "R");
						
						// default "-iM1L0"
						speakerModeVolume = String.Concat("-i", 
								speakerMode[Settings.EfaxModemSpeakerMode],
								speakerVolume[Settings.EfaxModemSpeakerVolume]);
						
						string [] tifFiles = Directory.GetFiles(sts[1], "tmp.*");
						StringBuilder filesToSend = new StringBuilder();
						foreach (string f in tifFiles) {
							filesToSend.Append(f);
							filesToSend.Append(" ");
						}
						
											
						ProcessStartInfo pidInfo = new ProcessStartInfo();
						pidInfo.FileName = "efax";
						pidInfo.RedirectStandardError = true;
						pidInfo.UseShellExecute = false;
						//used for testing
						//Thread.Sleep(5000);
						//pidInfo.FileName = "echo";
						//pidInfo.FileName = "sleep 5";										
						
						pidInfo.Arguments = String.Concat(
							"-d /dev/", Settings.EfaxModemDevice,	//modem port
							" -x ", Settings.EfaxLockfile, Settings.EfaxModemDevice,	// lockfile 
							" \"",  Settings.EfaxModemInit, "\"",		// init sequence
							" \"",  speakerModeVolume, "\"",	// speaker enable / mode
							" -l \"", Settings.FaxNumber, "\"",				// Our fax number
							" \"",  Settings.EfaxModemReset, "\"",	// how to reset modem
							" -f /usr/bin/efaxfont",
							" -h \'%d/%d\'", // should be name number and date
							" -v i",			// session progress information
							" -t T", sts[2],		// should make sure there are no bad chars
							" ", filesToSend);		// files to send
					    
						if (Settings.Faxtracing == true) {
							Console.WriteLine("[Efax.send]\n  {0}", pidInfo.Arguments);
						}
						Application.Invoke (delegate {gfax.GAppbar.ClearStack();});
						Application.Invoke (delegate {gfax.GAppbar.Push(Catalog.GetString("Sending facsimile..."));});	
						Process pid = Process.Start(pidInfo);
						StreamReader myStreamReader = pid.StandardError;
						
						//start the pulser
						Application.Invoke (delegate {gfax.Pulser.StartPulse();});
						Application.Invoke (delegate {gfax.Status.Append("");});	
						
						while (!pid.HasExited) {
							string str = myStreamReader.ReadLine();
							Application.Invoke (delegate {gfax.Status.Append(str);});	
							System.Threading.Thread.Sleep(200);
						}
						//stop the pulser.
						Application.Invoke (delegate {gfax.Pulser.EndPulse();});
																		
						if (Settings.Faxtracing == true) {
							Console.WriteLine("[Efax.efaxd] Exit code - {0}", pid.ExitCode);
						}
							
						switch (pid.ExitCode) {
							case 0:
								// If successful then mv the control file to the done queue and flag 
								// a date for it's removal.
								sts[10] = Catalog.GetString("Success");
								update_status_code(sts, controlFile, "D");
								// remove directory path
								string basefilename = controlFile.Remove(0, gfax.SpoolDirectory.Length + 1);
								string newfilename = String.Concat(gfax.SpoolDirectory,"/doneq/", basefilename);
								File.Move(controlFile, newfilename);
								break;
							case 1:
								// busy number, continue after timeout
								// update listview as well
								retries = Convert.ToInt32(sts[6]);
								if (retries++ < Settings.EfaxRetries) {
									sts[6] = retries.ToString();
									update_status_code(sts, controlFile, "W");
								}
								else {
									sts[10] = Catalog.GetString("Busy retries exceeded");
									update_status_code(sts, controlFile, "F");
								}
								break;
							case 2:
								// fatal errors - no retry
								//fatalError = true;
								// change code in file
								sts[10] = Catalog.GetString("Fatal error");
								update_status_code(sts, controlFile, "F");
								break;
							case 3:
								// Modem error - no retry
								//fatalError = true;
								sts[10] = Catalog.GetString("Fatal modem error");
								update_status_code(sts, controlFile, "F");
								break;
							case 4:
								// Modem not responding
								//fatalError = true;
								sts[10] = Catalog.GetString("Modem not responding");
								update_status_code(sts, controlFile, "B");
								break;
							case 5:
								// Program terminated
								//fatalError = true;
								sts[10] = Catalog.GetString("Program terminated");
								update_status_code(sts, controlFile, "F");
								break;
						}// end of switch
						
						//sleep between each fax for the modem to stablize
						System.Threading.Thread.Sleep(3000); 
					} // end of foreach
					// This basically sleeps between busy numbers.
					System.Threading.Thread.Sleep(30000); 
				}
				System.Threading.Thread.Sleep(30000); // sleep 30 seconds
				// Moved this from case 0 above
				if (Settings.Faxtracing == true) {
					Console.WriteLine("Deleting --->{0}", sts[1]);
				}
				//Directory.Delete(sts[1], true);
			}// end of while
		}
		
		
		// Updates the status code in the control file and writes it back to disk
		public void update_status_code (string[] job, string file, string status)
		{
			string fname = file;
						
			if ( status == "F" ) {
				string filename = file.Remove(0, gfax.SpoolDirectory.Length + 1);
				fname = String.Concat(gfax.SpoolDirectory,"/doneq/", filename);
				File.Delete(file);					
				Directory.Delete(job[1], true);
			}
			
			
			// Thread safe 
			TextWriter outfile = TextWriter.Synchronized(File.CreateText(fname));
			
			// TODO change this to string.concat and just issue one write
			try { 
				//outfile = File.CreateText(file); 
			
				outfile.Write("Jobid=");
				outfile.WriteLine(job[0]);
				outfile.Write("JobDirectory=");
				outfile.WriteLine(job[1]);
				outfile.Write("PhoneNumber=");
				outfile.WriteLine(job[2]);
				outfile.Write("Status=");
				outfile.WriteLine(status);
				outfile.Write("Owner=");
				outfile.WriteLine(job[4]);
				outfile.Write("Pages=");
				outfile.WriteLine(job[5]);
				outfile.Write("Dials=");
				outfile.WriteLine(job[6]);
				outfile.Write("Sendat=");
				outfile.WriteLine(job[7]);
				outfile.Write("Notification=");
				outfile.WriteLine(job[8]);
				outfile.Write("Email=");
				outfile.WriteLine(job[9]);
				outfile.Write("ErrorMessage=");
				outfile.WriteLine(job[10]);

				outfile.Close();
			}
			catch (Exception e) {
			  Console.WriteLine("[Efax.update_status_code] Exception: {0}", e);
			}
		}
		
	}
}
