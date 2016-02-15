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

//#define DEBUGHYLAFAX

namespace gfax {
	using Mono.Unix;
	using System;
	using System.IO;
	using System.Text;			//Encoding.ASCII.GetBytes
	using System.Collections;
	using System.Threading;
	using System.Net;
	using System.Net.Sockets;

	
	public class Hylafax
	{	
		TcpClient mainclient = new TcpClient();
		NetworkStream mainstream;
		string host, user, pass;
		int port;
		bool permission;
		
		
		public class StateObject
		{
			public TcpClient client = null;
			public int totalBytesRead = 0;
			public const int BufferSize = 256;
			public string readType = null;
			public byte[] buffer = new byte[BufferSize];
			public StringBuilder messageBuffer = new StringBuilder();
		}


		// connect
		//		return false on sucess or true on cancel
		public bool connect ()
		{	
			host = Settings.Hostname;
			port = Convert.ToInt32(Settings.Port);
			user = Settings.Username;
			if (gfax.hylafaxPassword == null)	// if we already entered a pass but
				if (Settings.Password != "")	// don't want to remember it it won't be null
					gfax.hylafaxPassword = Settings.Password;
							
			if (Settings.Faxtracing == true) {
				Console.WriteLine ("Initializing hylafax class .......");
			}
			
			try {
		    	mainclient.Connect(host, port);
          		mainstream = mainclient.GetStream();
				string s = read(mainstream, mainclient);
				if (Settings.Faxtracing == true) {
					Console.WriteLine ("[hylafax.connect] on connect : {0}", s);
				}
				
						
		    	if(mainstream.CanWrite && mainstream.CanRead){
          	    	// Write username
					if (Settings.Faxtracing == true) {
						Console.WriteLine ("[hylafax.connect] Sending USER : {0}", user);
					}
					
					//If the username is null then hylafax doesn't need usernames
					// TODO check the hylafax protocol about this
					if (user == "")
						user = "anonymous";
					
					write (mainstream, "USER " + user + "\n");					
					// Read result
					// If the result is ("230 User <username> logged in.")
					// 		I don't need to send a password
					// Else if the result is ("331 Password required for <username>.")
					//		I must send a password.
          	    	string returndata = read(mainstream, mainclient);
					
					if (returndata.Substring(0,3) == "331") {
						//Console.WriteLine("Password is {0}", gfax.hylafaxPassword);
						for (int i = 0; i < 3; i++) {
							
							if (gfax.hylafaxPassword == null) {
								G_Password gpass = new G_Password();
								gpass.Run();
							
								if (gpass.Cancel) {
									return false;  // connection cancelled
								}
								gfax.hylafaxPassword = gpass.Password;
								if (gpass.RememberPassword)
									Settings.Password = gpass.Password;
								gpass.Close();
							}
							// 530 Login incorrect. result from bad password
							//prompt for password
							write (mainstream, "PASS " + gfax.hylafaxPassword + "\n");
							
							string rtn = read(mainstream, mainclient);
							//Console.WriteLine("Return is {0}", rtn);
							if (rtn.Substring(0,3) == "230")  // user is logged in
								break;
							else {
								gfax.hylafaxPassword = null;
								write (mainstream, "USER " + user + "\n");					
          	    				string rtndata = read(mainstream, mainclient);
							}
						}
					}
					
					if (Settings.Faxtracing == true) {
						Console.WriteLine("[hylafax.connect] USER returned : {0}", returndata);
					}
				}
				else if (!mainstream.CanRead) {
					Console.WriteLine(Catalog.GetString("You can not write data to this stream"));
					mainclient.Close();
				}
				else if (!mainstream.CanWrite) {             
					Console.WriteLine(Catalog.GetString("You can not read data from this stream"));
					mainclient.Close();
				}
				
				return true;
			}
			catch (Exception e ) {
				Console.WriteLine(e.ToString());
				G_Message m = new G_Message(Catalog.GetString("Could not connect to your Hylafax server.\n" +
					"Check console messages for further information\n\n" +
					"You might need to set a value for username."));
				return false;
			}
		}
		
		public void close ()
		{
			mainclient.Close();
		}

		// Method status(queue)
		//
		//	queue is the queue directory of the hylafax server it can be one of:
		//	'status', 'sendq', 'doneq' or 'recvq'
		//
		// Return a list containing lines	
/*		
		public string status (string queue)
		{
			#if DEBUGHYLAFAX
				Console.WriteLine ("[hylafax.status] queue is : {0}", queue);
			#endif
			// try 
			return getfolder(queue);
		}
*/
		public void asyncstatus (string queue)
		{
			if (Settings.Faxtracing == true) {
				Console.WriteLine ("[hylafax.status] queue is : {0}", queue);
			}
			asyncgetfolder(queue);
		}

		//	send_init (string filename)
		//
		//	2) Store the filename on the server [storefile_open]
		//	3) Send the file line by line
		//	4) Close the stream for sending the file
		public string send_init (string fname)
		{
			G_ProgressBar pbar;
			TcpClient myclient;
			NetworkStream mystream;
			StreamReader fp = null;
			string buf;
			string remote_fname;
			double lines = 0;
			double lines_sent = 0;
			double percent = 0;
			
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[Hylafax.send_init] Filename : {0}", fname);
			}
			
			//timezone("LOCAL");
			// Setup the server and return a passive connection
			// PASV, STOT
			myclient = storefile_open();
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[Hylafax.send_init] Got tcpclient");
			}
			
			mystream = myclient.GetStream();
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[Hylafax.send_init] Stream is open");
			}
			//figure out how many lines in the file for progress bar
			// TODO progress bar and error 
			try { fp = File.OpenText(fname); }
			catch (Exception e) { 
			  Console.WriteLine("[hylafax.sendinit] Exception: {0}", e);
			}
			
			while ( (buf = fp.ReadLine()) != null ) {
				lines = lines + 1;
			}
			fp.Close();
			
			try { fp = File.OpenText(fname); }
			catch (Exception e) {  
			  Console.WriteLine("[hylafax.sendinit] Exception: {0}", e);
			}
			
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[Hylafax.send_init] File :{0} is open and has {1} lines", fname,lines);
			}
			
			pbar = new G_ProgressBar();
			//pbar.Text = "File xmit";
			lines_sent = 1;
			while ( (buf = fp.ReadLine()) != null ) {
				write(mystream, buf);
				write(mystream, "\n");
				lines_sent = lines_sent + 1;
				percent = lines_sent / lines;
				if (percent <= 1.0)
					pbar.Fraction = percent;
				//GLib.MainContext.Iteration ();
				while (Gtk.Application.EventsPending ())
                	Gtk.Application.RunIteration ();
				if (pbar.Cancel)
					break;
			}
			fp.Close();
			remote_fname = storefile_close(myclient);
			
			if (pbar.Cancel) {	//If the transfer was cancelled
				deletefile(remote_fname);
				remote_fname = "cancelled";
				pbar.Close();
			} else {
				pbar.Finished();
			}
			
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[Hylafax.send_init] Stream is closed, sent {0} lines",lines_sent);
			}
			return remote_fname;
		}
		
		// Method send(string filename_on_server, Contact contact)
		//
		// Sequence to send a file is:
		//		a) job_new				[job_new]
		//		b) set all job parms	[job_parm_set]
		//		c) submit the job		[submit_job]
		// format send time - should be as such:
		// yyyymmddhhmm
		//-> JPARM SENDTIME 200403100509
		//213 SENDTIME set to 20040310050900.
		public void send (string remote_fname, GfaxContact contact)
		{
			if (Settings.Faxtracing == true) {
				Console.WriteLine("Hylafax.send] top of method...");
			}

			string emailAddress = Settings.EmailAddress;
			if (Settings.Faxtracing == true) {
				Console.WriteLine("Hylafax.send] email address {0}", emailAddress);
			}

			string resolution = "98";
			string emailNotify = "none";
			
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[Hylafax.send] gfax.timeToSend : {0}", gfax.timeToSend);
				Console.WriteLine("[Hylafax.send] Remote file name is : {0}", remote_fname);
			}
			
			// if this is sent from GfaxSend wizard
			if (gfax.fromSendWizard) {			
				if (gfax.sendWizardResolution)
					resolution = "196";
			
				if (gfax.sendWizardEmailNotify) {
					emailNotify = "done";
					emailAddress = gfax.sendWizardEmailAddress;
				}
			} else {
				if (Settings.EmailNotify)
					emailNotify = "done";
					
				if (Settings.HiResolution)
					resolution = "196";
			}
			
			// Format time to send - timezone is in UTC format.
			string tts = String.Format("{0}{1:00}{2:00}{3:00}{4:00}",
					gfax.timeToSend.Year,
					gfax.timeToSend.Month,
					gfax.timeToSend.Day,
					gfax.timeToSend.Hour,
					gfax.timeToSend.Minute);
			
			string jid = job_new();
			//TODO try catch exception here
			//#item[1] is the name	#item[2] is the company 
			
			job_param_set("FROMUSER", Environment.UserName);
			job_param_set("LASTTIME", "000259");
			job_param_set("SENDTIME", tts);
			job_param_set("MAXDIALS", "12");
			job_param_set("MAXTRIES", "3");	
			job_param_set("SCHEDPRI", "127");		
			job_param_set("DIALSTRING", contact.PhoneNumber);
			job_param_set("NOTIFYADDR", emailAddress);
			job_param_set("VRES", resolution);		
			job_param_set("PAGEWIDTH", "215");
			job_param_set("PAGELENGTH", "279");
			job_param_set("NOTIFY", emailNotify);	//can be "none" or "done"
			job_param_set("PAGECHOP", "default");
			job_param_set("CHOPTHRESHOLD", "3");
			job_param_set("DOCUMENT", remote_fname);
			job_submit();
		}
		
		public bool getfile (string fname)
		{
			TcpClient myclient;
			NetworkStream mystream;
			FileStream fp;
			string data;
			double fsize = 0;
			double totalBytes = 0;
			double percent = 0;

			// get the file size for the receive progress bar
			write(mainstream, String.Concat("SIZE recvq/", fname, "\n"));
			data = read(mainstream, mainclient);
			try {
				string[] s = data.Split();
				fsize = Convert.ToDouble(s[1]);
			} catch (Exception e) {
				Console.WriteLine("Couldn't get file size");
				Console.WriteLine("Hylafax exception {0}", e);
				return (false);
			}
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[hylafax.getfile] \"File size is : {0}", fsize);
			}
			
			// assume we have permission to open file
			permission = true;
			// PASV, RETR
			myclient = recvfile_open(fname);
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[Hylafax.getfile] Got tcpclient");
			}
			
			mystream = myclient.GetStream();
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[Hylafax.getfile] Stream is open");
			}
			
			if (!permission) {
				return (false);
			}
			
			try {
				fp = new FileStream(String.Concat(gfax.SpoolDirectory,"/tif/",fname), FileMode.Create);
			} catch (Exception e) {
				Console.WriteLine(e.ToString());
				return(false);
			}
			
			byte[] bytes = new byte[256];
			
			do {
				try {
					gfax.MainProgressBar.Fraction = percent;
					while (Gtk.Application.EventsPending ())
                		Gtk.Application.RunIteration ();
						
					int len = mystream.Read(bytes, 0, bytes.Length);
					if (len > 0 ) {
						totalBytes = totalBytes + len;
						percent = totalBytes / fsize;
						if (percent <= 1.0)
							gfax.MainProgressBar.Fraction = percent;
						//GLib.MainContext.Iteration ();
						while (Gtk.Application.EventsPending ())
                			Gtk.Application.RunIteration ();
						fp.Write(bytes,0,len);
					} 
				} catch ( Exception e ) {
					Console.WriteLine(e.ToString());
					return(false);
				}
				// Seems if we don't sleep here for a bit, we never read
				// all the data???  Maybe a Hylafax thing.
				System.Threading.Thread.Sleep(1);
					
			} while (mystream.DataAvailable);
			
			fp.Close();
			recvfile_close(myclient);
			
			// valid file received
			return(true);
		}

			

		private string job_new ()
		{
			string data;
			string[] tmp;
			//string jobid;
			
			try {
				write(mainstream, "JOB default\n");
				data = read(mainstream, mainclient);
				if (Settings.Faxtracing == true) {
					Console.WriteLine("[hylafax.job_new] \"JOB default\" data returned : {0}", data);
				}

				write(mainstream, "JNEW\n");
				data = read(mainstream, mainclient);
				// data = 200 New job created: jobid: 14433 groupid: 14433.
				tmp = data.Split(' ');

				if (Settings.Faxtracing == true) {
					Console.WriteLine("[hylafax.job_new] \"JNEW\" data returned : {0}", data);
				}
				
			} catch (Exception e) {
				Console.WriteLine("Hylafax exception {0}", e);
				return ("");
			}
			
			// jobid might need to be Convert.ToInt32
			return tmp[5];
		}
		
		public void job_param_set (string pname, string pvalue)
		{
			string data;
		
			write(mainstream, "JPARM " + pname + " " + pvalue +"\n");
			data = read(mainstream, mainclient);	
			if (Settings.Faxtracing == true) {
				Console.Write("[hylafax.job_param_set] \"JPARM\" data returned : {0}", data);
			}
		}

		public void job_submit ()
		{
			string data;
			
			write(mainstream, "JSUBM\n");
			data = read(mainstream, mainclient);
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[hylafax.job_submit] \"JSUBM\" data returned : {0}", data);
			}
		}
		
		private TcpClient storefile_open ()
		{
			TcpClient myclient = new TcpClient();
			NetworkStream mystream;
			IPAddress ipaddr;
			int ipport;
			string data;
			
			write(mainstream, "PASV\n");	
			data = read(mainstream, mainclient);

			if (Settings.Faxtracing == true) {
				Console.WriteLine("[hylafax.storefile_open] data returned : {0}", data);
			}
			
			// dig out ip address and port for new connection
			ipaddr = get_ip_addr(data);
			ipport = get_ip_port(data);
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[hylafax.storefile_open] ipaddr, port : {0}, {1}", ipaddr, ipport);
			}
			
			try {
				myclient.Connect(ipaddr, ipport);
			} 
		    catch (Exception e ) {
				Console.WriteLine(e.ToString());
				// handle error here
			}
			
			mystream = myclient.GetStream();			
			write(mainstream, "STOT\n");
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[hylafax.storefile_open] wrote STOT");
			}
			data = read(mainstream, mainclient);
			//data = 150 FILE: /tmp/doc1097.ps (Opening new data connection).
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[hylafax.storefile_open] returned from STOT : {0}", data);
			}
			return myclient;
		}

		private string storefile_close(TcpClient tc)
		{
			string data;
			string[] s;
			string fn;
						
			tc.Close();
			data = read(mainstream, mainclient);
			// data = 226 Transfer complete (FILE: /tmp/doc1101.ps).
			// TODO error checking
			
			// remove white space first, Arrgggh!
			fn = data.Trim();
			s = fn.Split(' ');

			return(s[4].TrimEnd(')','.'));
		}

		private TcpClient recvfile_open (string fname)
		{
			TcpClient myclient = new TcpClient();
			NetworkStream mystream;
			IPAddress ipaddr;
			int ipport;
			string data;
			
			write(mainstream, "PASV\n");	
			data = read(mainstream, mainclient);
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[hylafax.recvfile_open] data returned : {0}", data);
			}

			// dig out ip address and port for new connection
			ipaddr = get_ip_addr(data);
			ipport = get_ip_port(data);
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[hylafax.recvfile_open] ipaddr, port : {0}, {1}", ipaddr, ipport);
			}
			
			try {
				myclient.Connect(ipaddr, ipport);
			} 
		    catch (Exception e ) {
				Console.WriteLine(e.ToString());
				// handle error here
			}
			
			mystream = myclient.GetStream();			
			write(mainstream, "TYPE I\n");	
			data = read(mainstream, mainclient);
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[hylafax.recvfile_open] data returned : {0}", data);
			}

			write(mainstream, String.Concat("RETR recvq/", fname, "\n"));
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[hylafax.recvfile_open] wrote RETR");
			}
			data = read(mainstream, mainclient);
			
			if (data.Substring(0,3) == "550")
				permission = false;
			//data = returned from RETR : 550 recvq/fax000000018.tif: Operation not permitted.
			//data = returned from RETR : 150 Opening new data connection for recvq/fax000000018.tif (2793 bytes).
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[hylafax.recvfile_open] returned from RETR : {0}", data);
			}
			return myclient;
		}

		private string recvfile_close(TcpClient tc)
		{
			string data;
			//string[] s;
			//string fn;
						
			tc.Close();
			data = read(mainstream, mainclient);
			// data = 226 Transfer complete (FILE: /tmp/doc1101.ps).
			// TODO error checking
			
			// remove white space first, Arrgggh!
			//fn = data.Trim();
			//s = fn.Split(' ');

			//return(s[4].TrimEnd(')','.'));
			return "";
		}

/*
		private string getfolder (string folder)
		{
			TcpClient myclient = new TcpClient();
			NetworkStream mystream;
			IPAddress ipaddr;
			int ipport;
			string jobfmt;
			
			write(mainstream, "PASV\n");	
			string data = read(mainstream, mainclient);
			
			// dig out ip address and port for new connection
			ipaddr = get_ip_addr(data);
			ipport = get_ip_port(data);

			try {
				myclient.Connect(ipaddr, ipport);
			} 
		    catch (Exception e ) {
				Console.WriteLine(e.ToString());
				// handle error here
			}
			
			mystream = myclient.GetStream();			
			/*
			RCVFMT (receive)
			%4p%1z %14.14s %7t %f
			p - number of pages
			z - * if still receiving, <space> otherwise
			s - sender (TIS)
			t - time received
			f - filename
			
			1= =Malaspina Univ=16Oct06=fax000000018.tif

			JOBFMT (send, done)
			j - Job identifier
			e - Phone number
			a - Job state (one-character symbol)
			o - Job owner
			P - # pages transmitted/total # pages to transmit
			D - Total # dials/maximum # dials
			s - Job status information from last failure
			Y - Scheduled date and time
			*/
/*			// set the job format string
			if (folder == "doneq" || folder == "sendq") {
				jobfmt = "\"%-4j=%-14e=%1a=%-12o=%-5P=%-5D=%.35s=%-19Y\"";
				write(mainstream, "JOBFMT "+jobfmt+"\n");
				data = read(mainstream, mainclient);
			}
			if (folder == "recvq") {
				jobfmt = "\"%4p=N%1z=%14.14s=%7t=%f\"";
				write(mainstream, "RCVFMT "+jobfmt+"\n");
				data = read(mainstream, mainclient);
			}
			write(mainstream, "LIST "+folder+"\n");
			data = read(mystream, myclient);
							
						
			#if DEBUGHYLAFAX
				Console.WriteLine("[hylafax.getfolder] list folder data : {0}", data);
				Console.WriteLine("[hylafax.getfolder] data length : {0}", data.Length);				
			#endif

			myclient.Close();
			return data;
		}
*/

		private void asyncgetfolder (string folder)
		{
			TcpClient myclient = new TcpClient();
			IPAddress ipaddr;
			int ipport;
			string jobfmt;
			
			write(mainstream, "PASV\n");	
			string data = read(mainstream, mainclient);
			
			// dig out ip address and port for new connection
			ipaddr = get_ip_addr(data);
			ipport = get_ip_port(data);

			try {
				myclient.Connect(ipaddr, ipport);
			} 
		    catch (Exception e ) {
				Console.WriteLine(e.ToString());
				// handle error here
			}
			
			/*
			RCVFMT (receive)
			%4p%1z %14.14s %7t %f
			p - number of pages
			z - * if still receiving, <space> otherwise
			s - sender (TIS)
			t - time received
			f - filename
			
			1= =Malaspina Univ=16Oct06=fax000000018.tif

			JOBFMT (send, done)
			j - Job identifier
			e - Phone number
			a - Job state (one-character symbol)
			o - Job owner
			P - # pages transmitted/total # pages to transmit
			D - Total # dials/maximum # dials
			s - Job status information from last failure
			Y - Scheduled date and time
			*/
			// set the job format string
			if (folder == "doneq" || folder == "sendq") {
				jobfmt = "\"%-4j=%-14e=%1a=%-12o=%-5P=%-5D=%.35s=%-19Y\"";
				write(mainstream, "JOBFMT "+jobfmt+"\n");
				data = read(mainstream, mainclient);
			}
			if (folder == "recvq") {
				jobfmt = "\"%4p=N%1z=%28.28s=%Y=%f\"";
				write(mainstream, "RCVFMT "+jobfmt+"\n");
				data = read(mainstream, mainclient);
			}
			write(mainstream, "LIST "+folder+"\n");
			//data = read(mystream, myclient);
			asyncread(myclient, folder);
		}

		private IPAddress get_ip_addr (string ipdata)
		{
			int index = ipdata.IndexOf("(");
			int len = ipdata.IndexOf(")") - index;
			string s = ipdata.Substring(index + 1, len - 1 );
			char[] splitter  = {','};
			string[] sa = s.Split(splitter);
			string sipaddr = String.Concat(sa[0],".",sa[1],".",sa[2],".",sa[3]);
			return IPAddress.Parse(sipaddr);
		}
		
		private int get_ip_port (string ipdata)
		{
			// find port returned
			int index = ipdata.IndexOf("(");
			int len = ipdata.IndexOf(")") - index;
			string s = ipdata.Substring(index + 1, len - 1 );
			char[] splitter  = {','};
			string[] sa = s.Split(splitter);
			return (Convert.ToInt32(sa[4]) * 256 + Convert.ToInt32(sa[5]));
		}

		
		private void write (NetworkStream sock, string s)
		{
			byte[] bytes = Encoding.ASCII.GetBytes(s);
			sock.Write(bytes, 0, bytes.Length);
		}

		
		private string read (NetworkStream str, TcpClient c)
		{
			StringBuilder buf = new StringBuilder();
			byte[] bytes = new byte[256];
						
			do {
				try {
					int len = str.Read(bytes, 0, bytes.Length);
					if (len > 0 ) {
						buf.Append(Encoding.ASCII.GetString(bytes,0,len));
					} 
				} catch ( Exception e ) {
					Console.WriteLine("Network IO problem " + e.ToString());
				}
				// Seems if we don't sleep here for a bit, we never read
				// all the data???  Maybe a Hylafax thing.
				System.Threading.Thread.Sleep(5);
	
			} while (str.DataAvailable);
			return (buf.ToString()); 
		}			


		//public void asyncread (TcpClient sock, string readType)
		private void asyncread (TcpClient sock, string readType)
		{
			gfax.activeNetwork = true;
			if (readType == "status") {
				gfax.asyncServerMessageBuffer = "";
			} else {
				gfax.asyncFolderMessageBuffer = "";
			}
			
			StateObject state = new StateObject();
			state.client = sock;
			state.readType = readType;
			NetworkStream stream = sock.GetStream();
			
			if (stream.CanRead) {
				try {
					IAsyncResult ar = stream.BeginRead (state.buffer, 0, StateObject.BufferSize, 
							new AsyncCallback(myReadCallBack), state);
				} catch ( Exception e ) {
					Console.WriteLine("Network IO problem " + e.ToString());
				}
			}
		}
		
		
		//========================================================
		//public static void myReadCallBack(IAsyncResult ar )
		private static void myReadCallBack(IAsyncResult ar )
		{
			int numberOfBytesRead;
			StateObject state = (StateObject) ar.AsyncState;
			NetworkStream mas = state.client.GetStream();
			string type = null;
			
			numberOfBytesRead = mas.EndRead(ar);
			state.totalBytesRead += numberOfBytesRead;
			
			//Console.WriteLine("Byes read ---------------> {0}", numberOfBytesRead);
			if ( numberOfBytesRead > 0) {
				state.messageBuffer.Append(Encoding.ASCII.GetString(state.buffer, 0, numberOfBytesRead));    
				mas.BeginRead(state.buffer, 0, StateObject.BufferSize, 
                        new AsyncCallback(myReadCallBack), state);  
			} else {
				mas.Close();
				state.client.Close();
				//Console.WriteLine("Byes read ---------------> {0}", state.totalBytesRead);
				//Console.WriteLine("You received the following message : {0} " + state.messageBuffer);
				if (state.readType == "status") {
					gfax.asyncServerMessageBuffer = state.messageBuffer.ToString();
				} else {
					gfax.asyncFolderMessageBuffer = state.messageBuffer.ToString();
				}
				gfax.asyncReadType = state.readType;
				mas = null;
				state = null;
				gfax.activeNetwork = false;
			}
		}

		public void job_select (string jobid)
		{
			write(mainstream, "JOB " + jobid + "\n");
			string s = read(mainstream, mainclient);
			#if DEBUGHYLAFAX
				Console.WriteLine("[hylafax.select] returned: {0}", s);
			#endif
		}
		
		public void job_kill ()
		{
			write (mainstream, "JKILL\n");
			string s = read(mainstream, mainclient);
			#if DEBUGHYLAFAX
				Console.WriteLine("[hylafax.kill] returned: {0}", s);
			#endif
		}
		
		public void job_delete ()
		{
			write (mainstream, "JDELE\n");
			string s = read(mainstream, mainclient);
			#if DEBUGHYLAFAX
				Console.WriteLine("[hylafax.delete] returned: {0}", s);
			#endif
		}

		public void job_suspend ()
		{
			write (mainstream, "JSUSP\n");
			string s = read(mainstream, mainclient);
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[hylafax.suspend] returned: {0}", s);
			}
		}
		
		// Returns
		// 550 fax000000019.tif: Operation not permitted.
		//
		public int deletefile (string name)
		{
			write (mainstream, String.Concat("DELE ", "recvq/", name, "\n"));
			string s = read(mainstream, mainclient);
			if (s.Substring(0,3) == "550")
				return(1);
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[hylafax.deletefile] returned: {0}", s);
			}
			return (0);
		}


		private void job_param (string parms)
		{
			write (mainstream, "JPARM " + parms + "\n");
			string s = read(mainstream, mainclient);
			#if DEBUGHYLAFAX
				Console.WriteLine("[hylafax.param] returned: {0}", s);
			#endif
		}

		public void job_reset (string parms)
		{
			write (mainstream, "JREST " + parms + "\n");
			string s = read(mainstream, mainclient);
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[hylafax.reset] returned: {0}", s);
			}

		}


		private void timezone (string name)  // can be "LOCAL" or "GMT"
		{
			write (mainstream, "TZONE " + name + "\n");
			string s = read(mainstream, mainclient);
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[hylafax.timezone] returned: {0}", s);
			}
		}
	}
}
