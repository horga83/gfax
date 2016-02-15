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

//#define DEBUGHYLAFAX


namespace gfax {
	using Mono.Unix;
	using System;
	using System.Collections;

	public enum FaxStatus: int {
		Sent,
		Fail,
		New,
		Busy,
		Block,
		Sleep,
		Run,
		Receiving,
		Received
	};

	public class Fax
	{
		static Hylafax hfax;
		static Efax efax;
		static bool firstRun = true;

		public class FaxQueue
		{
			public string Jobid;
			public string Number;
			public string Status;
			public FaxStatus StatusType;
			public string Owner;
			public string Pages;
			public string Dials;
			public string Error;
			public object Sendat;
		}
		
		public class FaxRecQueue
		{
			public string Pages;
			public string Status;
			public FaxStatus StatusType;
			public string Sender;
			public object TimeReceived;			
			public string Filename;			
		}
		
		public static string async_get_server_status ()
		{
						
			if (Settings.TransmitAgent == "hylafax") {
				hfax = new Hylafax();
				
				if ( !hfax.connect() )
					return (Catalog.GetString("No Connection"));
				hfax.asyncstatus("status");
				hfax.close();
				hfax = null;
				return ("");
			}
			
			if (Settings.TransmitAgent == "efax") {
				//TODO put modem ready status here
				if (firstRun) {
					firstRun = false;					
					return (Catalog.GetString("Efax transmit process running...\nScanning job files every 30 seconds."));
				}
				else
					return (null);
			}
			
			return (Catalog.GetString("Error transport agent not specified!"));
		}

		public static ArrayList async_get_queue_status (string queue)
		{
			if (Settings.TransmitAgent == "hylafax") {
				hfax = new Hylafax();
				
				if ( hfax.connect() )
					hfax.asyncstatus(queue);
				hfax.close();
				hfax = null;
			}		
			
			if (Settings.TransmitAgent == "efax") {
				string reply = gfax.efax.status(queue);
				
				if (queue == "doneq" || queue == "sendq")
					return (parse_senddone(reply));
				else // (queue == "recvq")
					return (parse_receive(reply));
			}
			return (null);
		}

		// parse the send or done queue
		public static ArrayList parse_senddone (string reply)
		{
			ArrayList list = new ArrayList();
			
			if (reply.Length > 0) {
				foreach (string s in reply.Split('\n')) {
					FaxQueue hq = new FaxQueue();
					//Console.WriteLine("s length equals --> {0}", s.Length);				
					if (s.Length == 0  )
						break; 
    	    		
    	    		string[] sa = s.Split('=');
					
					// TODO put error here about bad network comms
					if (sa.Length != 8) {
						break;
					}
					
					if ( sa[0].Length != 0 )
						hq.Jobid = sa[0].Trim();
					else
						hq.Jobid = "";
					if ( sa[1].Length != 0 )
						hq.Number = sa[1].Trim();
					else
						hq.Number = "";	
					if ( sa[2].Length != 0 ) {
						switch (sa[2]) {
							case "R":
								hq.Status = Catalog.GetString("Run");
								hq.StatusType = FaxStatus.Run;
							break;
							case "S":
								hq.Status = Catalog.GetString("Sleep");
								hq.StatusType = FaxStatus.Sleep;
							break;
							case "B":
								hq.Status = Catalog.GetString("Block");
								hq.StatusType = FaxStatus.Block;
							break;
							case "W":
								hq.Status = Catalog.GetString("Busy");
								hq.StatusType = FaxStatus.Busy;
							break;
							case "D":
								hq.Status = Catalog.GetString("Done");
								hq.StatusType = FaxStatus.Sent;
							break;
							case "F":
								hq.Status = Catalog.GetString("Fail");
								hq.StatusType = FaxStatus.Fail;
							break;
							case "P":
								hq.Status = Catalog.GetString("New");
								hq.StatusType = FaxStatus.New;
							break;
						}
					}
					else
						hq.Status = "";
					if ( sa[3].Length != 0 )
						hq.Owner = sa[3].Trim();
					else
						hq.Owner = "";	
					if ( sa[4].Length != 0 )
						hq.Pages = sa[4].Trim();
					else
						hq.Pages = "";
					if ( sa[5].Length != 0 )
						hq.Dials = sa[5].Trim();
					else
						hq.Dials = "";	
					if ( sa[6].Length != 0 )
						hq.Error = sa[6];
					else
					hq.Error = "";
					
					if ( sa[7].Length != 0 ) { // 2004/03/09 18.01.51
						try {
							hq.Sendat = (DateTime)System.DateTime.ParseExact(sa[7].Replace(".", ":").Trim(), "yyyy/MM/dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture).ToLocalTime();
						}
						catch(FormatException e)
						{
	            Console.WriteLine("[fax.parse_senddone] Exception: {0}", e);
							hq.Sendat = null;
						}
					} else {
						hq.Sendat = null;
					}
					
					list.Add(hq);
				}
				
			} else  {		// no status
			
				FaxQueue hq = new FaxQueue();
				hq.Jobid = "";
				hq.Number = "";
				hq.Status = "";
				hq.Owner = "";
				hq.Pages = "";
				hq.Dials = "";
				hq.Error = "";
				hq.Sendat = null;
				
				list.Add(hq);
			} 
        
			return (list);
		}

		// parse the receive queue
		//
		//Page S Sender/TSI  Recvd@ Filename
		//1 +49 30 7865224 10Nov04 fax00004.tif
		// should come back as
		//1= =+49 30 7865224=10Nov04=fax00004.tif
		public static ArrayList parse_receive (string reply)
		{
			ArrayList list = new ArrayList();
			
			Console.WriteLine("[fax.parse_receive] reply -> {0}", reply);
			Console.WriteLine("[fax.parse_receive] End of reply----------------");
			if (reply.Length > 0) {
				foreach (string s in reply.Split('\n')) {
					FaxRecQueue hq = new FaxRecQueue();
					if (s.Length == 0 )
						break;
						
    	    		string[] sa = s.Split('=');
    	    		if (sa.Length != 5) {
						break;
					}
					
					if ( sa[0].Length != 0 )
						hq.Pages = sa[0].Trim();
					else
						hq.Pages = "";	
					if ( sa[1].Length != 0 )
						switch (sa[1]) {
							case "N*":
								hq.Status = Catalog.GetString("Receiving");
								hq.StatusType = FaxStatus.Receiving;
							break;
							case "N ":
								hq.Status = Catalog.GetString("Done");
								hq.StatusType = FaxStatus.Received;
							break;
						}
					if ( sa[2].Length != 0 )
						hq.Sender = sa[2].Trim();
					else
						hq.Sender = "";
					if ( sa[3].Length != 0 )
					{
						Console.WriteLine("[fax.parse_receive] sa[3] -> {0}", sa[3]);
						try {
							hq.TimeReceived = (DateTime)System.DateTime.ParseExact(sa[3], "yyyy:MM:dd hh:mm:ss", System.Globalization.CultureInfo.InvariantCulture).ToLocalTime();
						}
						catch(FormatException e )
						{          
						  Console.WriteLine("[fax.parse_receive] Exception: {0}", e);
							hq.TimeReceived = (DateTime)System.DateTime.ParseExact("1970:01:01 00:00:00", "yyyy:MM:dd hh:mm:ss",System.Globalization.CultureInfo.InvariantCulture);
						}
					}
					else
						hq.TimeReceived = (DateTime)System.DateTime.ParseExact("1970:01:01 00:00:00", "yyyy:MM:dd hh:mm:ss",System.Globalization.CultureInfo.InvariantCulture);	
					if ( sa[4].Length != 0 )
						hq.Filename = sa[4].Trim();
					else
						hq.Filename = "";
					list.Add(hq);
				}
				
			} else  {		// no status
			
				FaxRecQueue hq = new FaxRecQueue();
				hq.Pages = "";
				hq.Status = "";
				hq.Sender = "";
				hq.TimeReceived = null;
				hq.Filename = "";
				
				list.Add(hq);
			} 
        
			return (list);
		}

		
		
		public static void sendfax (string fname)
		{
			string remote_fname;
			
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[Fax.sendfax] File name is : {0}", fname);
			}
			
			if (Settings.TransmitAgent == "hylafax") {
				Hylafax hfaxsf = new Hylafax();
				// hylafax actually stores the file to the server
				hfaxsf.connect();
				remote_fname = hfaxsf.send_init(fname);
				
				// if "Cancel" button pressed on progess bar
				if (remote_fname == "cancelled") 
					return;
				
				if (Settings.Faxtracing == true) {
					if (Settings.TransmitAgent == "hylafax") {
						Console.WriteLine("[Fax.sendfax] Remote file name is : {0}", remote_fname);
					}
				}
				
				//System.Threading.Thread.Sleep(2000);
				
				IEnumerator enu = gfax.Destinations.GetEnumerator();
				
				if (Settings.Faxtracing == true) {
					if (Settings.TransmitAgent == "hylafax") {
						Console.WriteLine("[Fax.sendfax] Destinations has a count of : {0}", gfax.Destinations.Count);
					}
				}
    	  		
				while ( enu.MoveNext() ) {
    	     		// TODO try catch exception here
					GfaxContact c = (GfaxContact)enu.Current;
					
				if (Settings.Faxtracing == true) {
					if (Settings.TransmitAgent == "hylafax") {
						Console.WriteLine("[Fax.sendfax] In while loop contact is -----> {0}", c.PhoneNumber);
					}
				}
					
					hfaxsf.send(remote_fname, c);
					
				if (Settings.Faxtracing == true) {
					if (Settings.TransmitAgent == "hylafax") {
						Console.WriteLine("[Fax.sendfax] In while loop bottom... ");
					}
				}
				
					//System.Threading.Thread.Sleep(1000);
					// open the log and log out going fax to server
					// Date Time PhoneNumber Organization ContactPerson
					// log file is in ~/.etc/gfax
					//if (Settings.LogEnabled)
						//log_it((GfaxContact)enu.Current);
				}
				
				if (Settings.Faxtracing == true) {
					if (Settings.TransmitAgent == "hylafax") {
						Console.WriteLine("[Fax.sendfax] End of send contact loop...");
					}
				}
				
				hfaxsf = null;
			} 
			
			
			//Efax transport
			if (Settings.TransmitAgent == "efax") {
								
				// Convert the file with ghostscript
				string directory = gfax.efax.send_init(fname);
				if (directory == "cancelled") 
					return;
				
				IEnumerator enu = gfax.Destinations.GetEnumerator();
    	  		while ( enu.MoveNext() ) {
					gfax.efax.send(directory, (GfaxContact)enu.Current);
					if (Settings.LogEnabled)
						log_it((GfaxContact)enu.Current);
				}
			}
		}

		public static bool recvfax (string fname)
		{
							
			// Hylafax support
			if (Settings.TransmitAgent == "hylafax") {
				hfax = new Hylafax();
				// hylafax actually stores the file to the server
				hfax.connect();
				return(hfax.getfile(fname));
			} 

			// Efax transport
			if (Settings.TransmitAgent == "efax") {
				return false;
			}
			
			return false;
		}


		public static void log_it (GfaxContact contact)
		{
		
		}


		public static void modify_job (string jobid, string number, string sendat, string dials)
		{
			if (Settings.TransmitAgent == "hylafax") {
				hfax = new Hylafax();
				hfax.connect();
				hfax.job_select(jobid);
				hfax.job_suspend();
				hfax.job_param_set("SENDTIME", sendat);
				hfax.job_param_set("EXTERNAL", number);				
				hfax.job_param_set("DIALSTRING", number);
				hfax.job_param_set("MAXDIALS", dials);
				hfax.job_submit();
				hfax = null;
			}

			/*
			if (Settings.TransmitAgent == "efax") {
				gfax.efax.job_kill(jobid);
				gfax.efax.job_delete(jobid);
			}
			*/

		}
		
		public static void resubmit_job (string jobid, string number, string sendat, string dials)
		{
			// will have to query the job, get the file name
			// possibly retrieve the file and then re-submit the job
			
			if (Settings.TransmitAgent == "hylafax") {
				hfax = new Hylafax();
				hfax.connect();
				hfax.job_select(jobid);
				
				/*
				hfax.job_reset("P");
				hfax.job_suspend();
				hfax.job_param_set("SENDTIME", sendat);
				hfax.job_param_set("EXTERNAL", number);				
				hfax.job_param_set("DIALSTRING", number);
				hfax.job_param_set("MAXDIALS", dials);
				hfax.job_submit();
				hfax = null;
				*/
			}
			
		}


		public static void delete_job (string jobid)
		{
			if (Settings.TransmitAgent == "hylafax") {
				hfax = new Hylafax();
				hfax.connect();
				hfax.job_select(jobid);
				hfax.job_kill();
				hfax.job_delete();
				hfax = null;
			}
			
			if (Settings.TransmitAgent == "efax") {
				gfax.efax.job_kill(jobid);
				gfax.efax.job_delete(jobid);
			}

		}
		
		public static int delete_file (string fname)
		{
			if (Settings.TransmitAgent == "hylafax") {
				hfax = new Hylafax();
				hfax.connect();
				int reply = hfax.deletefile(fname);
				hfax = null;
				return (reply);
			}
			
			if (Settings.TransmitAgent == "efax") {
				return(0);
			}
			return(0);
		}
	}
}
