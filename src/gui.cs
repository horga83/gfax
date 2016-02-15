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

//#define DEBUG

namespace gfax {
	using Mono.Unix;
	using System;
	using System.IO;
	using GLib;
	using Gdk;
	using Gtk;
	using Gnome;
	using Glade;
	using GConf.PropertyEditors;
	using GtkSharp;
	using System.Runtime.InteropServices;
	using System.Collections;
	using System.Diagnostics;
	using System.Reflection;

	
	//************************************************************************
	// Gfax class
	//
	// This is the main application window
	
	public class Gfax : Program
	{
		const string VERSION = "0.7.7";
		const string NAME = "Gfax";
		const string APPNAME = "Gfax";
		
		// Notebook pages
		const int SENDQ_PAGE = 0;
		const int DONEQ_PAGE = 1;
		const int RECEIVEQ_PAGE = 2;
		
		const int COLUMN_0 = 0;
		const int COLUMN_1 = 1;
		const int COLUMN_2 = 2;
		const int COLUMN_3 = 3;
		const int COLUMN_4 = 4;		
		const int COLUMN_5 = 5;				
		const int ALL_COLUMNS = -1;	
	
		// Strings to hold old contact info temporarily
		string tempName;
		string tempNumber;
		string tempCompany;
		
		ArrayList oldSendQueue = null;		// hold old queue results
		
		bool restart = true;
		bool eventsEnabled = true;
				
		enum ActiveQ { done, send, receive };
		ActiveQ activeQ;
		
		
		Glade.XML gxml;
		
		[Glade.Widget] Gtk.Window GfaxWindow;
		[Glade.Widget] Gnome.AppBar Appbar;
						
		[Glade.Widget] Gtk.TreeView StatusList;
		[Glade.Widget] Gtk.TreeView JobsCompleteList;
		[Glade.Widget] Gtk.TreeView JobsReceivedList;		
		[Glade.Widget] Gtk.Notebook StatusNotebook;
		[Glade.Widget] Gtk.Button DeleteJobButton;
		[Glade.Widget] Gtk.Button ModifyJobButton;	
		[Glade.Widget] Gtk.Button ViewPrintButton;
		[Glade.Widget] Gtk.Button RecvfaxDeleteButton;
		[Glade.Widget] Gtk.TextView StatusText;
		
			
		// Menu items
		[Glade.Widget] Gtk.CheckMenuItem AutoQRefreshCheckMenuItem;
		[Glade.Widget] Gtk.CheckMenuItem EmailNotificationCheckMenuItem;
		[Glade.Widget] Gtk.CheckMenuItem HiResolutionModeCheckMenuItem;
		[Glade.Widget] Gtk.CheckMenuItem LogEnabledCheckMenuItem;
		[Glade.Widget] Gtk.CheckMenuItem FaxTracingCheckMenuItem;
		
		//Gtk.Window MainWindow;
				
		Gtk.ListStore StatusStore;
		Gtk.ListStore RecvStore;
		Gtk.TreeModel StatusModel;
		G_ListView lv, jobsCompletedView, jobsReceivedView;
		
		// text buffer for status text area
		TextBuffer StatusTextBuffer = new TextBuffer(new TextTagTable());
				
		Phonebook[] myPhoneBooks;
		
		GConf.PropertyEditors.EditorShell shell;

		static bool iconified = true;	// gfax starts iconified
		
		long filePos = 0;
		static long lastMaxOffset;
		
				
				
		public Gfax (string fname, string[] args)
			: base (APPNAME, VERSION, Modules.UI, args, new object [0])
		{
			//Phonebook[] pb;  delete me

 			// Set the program icon
 			Gtk.Window.DefaultIconName = "gfax";
 
			Application.Init ();

			// check to see if we've run before, if so gfax will be there
			if ( Settings.RunSetupAtStart ) {
				Settings.RunSetupAtStart = false;
				
				MessageDialog md;
				md = new MessageDialog (
					null,
					DialogFlags.DestroyWithParent, 
					MessageType.Info, 
					ButtonsType.Ok,
					Catalog.GetString(
						@"
This is the first time you have run Gfax.
You should set your MODEM TYPE and PORT under preferences.

Gfax is initially setup to use Efax, you may change it use 
Hylafax if you prefer or require connection to a network 
facsimile server.")
				);
				md.Run ();
				md.Destroy();
			}
			
			if (!Directory.Exists(gfax.SpoolDirectory)) {
				G_Message gm = new G_Message(Catalog.GetString(
					@"Your spool directory is missing!
					
Please login as the root user and create the "
+ gfax.SpoolDirectory + 
" directory.\n\nAll users should be able to write to it.\n"));
				return;
			}
			if (!Directory.Exists(gfax.SpoolDirectory + "/doneq")) {
				G_Message gm = new G_Message(Catalog.GetString(
					@"The doneq directory is missing in your spool directory!
					
Please login as the root user and create the "
+ gfax.SpoolDirectory + "/doneq" +
" directory.\n\nAll users should be able to write to it.\n"));
				return;
			}
			if (!Directory.Exists(gfax.SpoolDirectory + "/recq")) {
				G_Message gm = new G_Message(Catalog.GetString(
					@"The recq directory is missing in your spool directory!
					
Please login as the root user and create the "
+ gfax.SpoolDirectory + "/recq" +
" directory.\n\nAll users should be able to write to it.\n"));
				return;
			}

			
			gxml = new Glade.XML (null, "gfax.glade", "GfaxWindow", null);
			gxml.Autoconnect (this);


			// Set initial gui state as per preferences
			// GConf.PropertyEditors.EditorShell doesn't handle 
			// checkmenuitems;
			eventsEnabled = false;
		 	if (Settings.TransmitAgent == "hylafax") {
				AutoQRefreshCheckMenuItem.Active = Settings.RefreshQueueEnabled;
				EmailNotificationCheckMenuItem.Active = Settings.EmailNotify;
				HiResolutionModeCheckMenuItem.Active = Settings.HiResolution;
				LogEnabledCheckMenuItem.Active = Settings.LogEnabled;
			} 
			if (Settings.TransmitAgent == "efax") {
				AutoQRefreshCheckMenuItem.Active = Settings.RefreshQueueEnabled;
				AutoQRefreshCheckMenuItem.Sensitive = false;
				EmailNotificationCheckMenuItem.Active = false;
				EmailNotificationCheckMenuItem.Sensitive = false;
				HiResolutionModeCheckMenuItem.Active = Settings.HiResolution;
				LogEnabledCheckMenuItem.Visible = false;
			}		

			FaxTracingCheckMenuItem.Active = Settings.Faxtracing;

			eventsEnabled = true;
				
			StatusText.Editable = false;
			StatusText.CanFocus = false;
			StatusText.Buffer = StatusTextBuffer;	

			// Set the program icon
			Gdk.Pixbuf Icon = new Gdk.Pixbuf(null, "gfax.png");
		
			gfax.MainWindow = GfaxWindow;

 			// Setup listview icons
 			InitListViewIcons();
			
			StatusStore = new ListStore(
 						typeof (Gdk.Pixbuf),
						typeof (string),
						typeof (string),
						typeof (string),
						typeof (string),
						typeof (string),
						typeof (string),
 						typeof (DateTime),						
						typeof (string));
			
			RecvStore = new ListStore(
 						typeof (Gdk.Pixbuf),			
						typeof (string),
						typeof (string),
						typeof (string),
 						typeof (DateTime),						
						typeof (string));


			lv = new G_ListView(StatusList, StatusStore);
 			lv.AddColumnIcon(Gtk.Stock.Info, 0);
 			lv.AddColumnTitle(Catalog.GetString("Jobid"),	1, 1);
 			lv.AddColumnTitle(Catalog.GetString("Number"),	2, 2);
 			lv.AddColumnTitle(Catalog.GetString("Status"),	3, 3);
 			lv.AddColumnTitle(Catalog.GetString("Owner"),	4, 4);
 			lv.AddColumnTitle(Catalog.GetString("Pages"),	5, 5);
 			lv.AddColumnTitle(Catalog.GetString("Dials"),	6, 6);
 			lv.AddColumnDateTime(Catalog.GetString("Send At"), "G",	7, 7);
 			lv.AddColumnTitle(Catalog.GetString("Information"),	8, 8);
			
			// List view for completed jobs tab
			jobsCompletedView = new G_ListView(JobsCompleteList, StatusStore);
 			jobsCompletedView.AddColumnIcon(Gtk.Stock.Info, 0);
 			jobsCompletedView.AddColumnTitle(Catalog.GetString("Jobid"),	1, 1);
 			jobsCompletedView.AddColumnTitle(Catalog.GetString("Number"),	2, 2);
 			jobsCompletedView.AddColumnTitle(Catalog.GetString("Status"),	3, 3);
 			jobsCompletedView.AddColumnTitle(Catalog.GetString("Owner"),	4, 4);
 			jobsCompletedView.AddColumnTitle(Catalog.GetString("Pages"),	5, 5);
 			jobsCompletedView.AddColumnTitle(Catalog.GetString("Dials"),	6, 6);
 			jobsCompletedView.AddColumnDateTime(Catalog.GetString("Send At"), "G",	7, 7);
 			jobsCompletedView.AddColumnTitle(Catalog.GetString("Information"),	8, 8);

			jobsReceivedView = new G_ListView(JobsReceivedList, RecvStore);
 			jobsReceivedView.AddColumnIcon(Gtk.Stock.Info, 0);
 			jobsReceivedView.AddColumnTitle(Catalog.GetString("Sender"),	1, 1);
 			jobsReceivedView.AddColumnTitle(Catalog.GetString("Status"),	2, 2);
 			jobsReceivedView.AddColumnTitle(Catalog.GetString("Pages  "),	3, 3);
 			jobsReceivedView.AddColumnDateTime(Catalog.GetString("Arrived"), "G",	4, 4);
 			jobsReceivedView.AddColumnTitle(Catalog.GetString("Filename"),	5, 5);
			
			StatusList.Selection.Changed += 
				new EventHandler(on_StatusList_selection);					
			StatusList.Selection.Mode = SelectionMode.Multiple;				
			StatusList.HeadersVisible = true;
			
			JobsCompleteList.Selection.Changed += 
				new EventHandler(on_JobsCompleteList_selection);
			JobsCompleteList.Selection.Mode = SelectionMode.Multiple;				
			JobsCompleteList.HeadersVisible = true;
			
			JobsReceivedList.Selection.Changed += 
				new EventHandler(on_JobsReceivedList_selection);
			JobsReceivedList.Selection.Mode = SelectionMode.Multiple;				
			JobsReceivedList.HeadersVisible = true;

			// Make sure headers are visible
			lv.AddTextToRow(null,"","","","","","",null,"");
			jobsCompletedView.AddTextToRow(null,"","","","","","",null,"");
			jobsReceivedView.AddTextToRow(null,"","","",null,"");
			StatusStore.Clear();
			RecvStore.Clear();
			
			DeleteJobButton.Sensitive = false;
			if (Settings.TransmitAgent == "hylafax")
				ModifyJobButton.Sensitive = false;
			else
				ModifyJobButton.Visible = false;
			ViewPrintButton.Sensitive = false;
				
			// Setup some global variables
			gfax.MainProgressBar = Appbar.Progress;
			gfax.GStatusTextBuffer = StatusTextBuffer;
			gfax.GAppbar = Appbar;
			
			gfax.Pulser = new Pulser();
			gfax.Status = new Status(StatusText, StatusTextBuffer);
			
			if (Settings.RefreshQueueEnabled)
				GLib.Timeout.Add((uint)(Settings.RefreshQueueInterval * 1000), 
					new TimeoutHandler (queue_refresh));
			
			async_update_status();
			
			if (Settings.TransmitAgent == "hylafax" ) {
				while (gfax.activeNetwork)
					System.Threading.Thread.Sleep(100);

				async_net_read_finished();
			}
			
			activeQ = ActiveQ.send;
			async_update_queue_status("sendq");
		
			StatusIcon sicon = new StatusIcon(Icon);
			sicon.Activate += new EventHandler (OnImageClick);			
			sicon.Tooltip = "Gfax Facsimile Sender";
			// showing the trayicon
			sicon.Visible = true;
			// setup system tray icon
			gfax.MainWindow.SkipTaskbarHint = true;
			gfax.MainWindow.Iconify();
			
			Application.Run ();
		}

		// end up here if we click the icon in the notifier
		private static void OnImageClick(object o, EventArgs arg)
		{
			if (iconified) {
				gfax.MainWindow.Deiconify();
				iconified = false;
				gfax.MainWindow.SkipTaskbarHint = false;
			} else {
				gfax.MainWindow.Iconify();
				gfax.MainWindow.SkipTaskbarHint = true;				
				iconified = true;
			}
		}
		
		// End up here if we iconify with the window manager
		private static void on_mainWindow_window_state_event(object o, WindowStateEventArgs args)
		{
			//Console.WriteLine ("state event: type=" + args.Event.Type + 
			//	"; new_window_state=" + args.Event.NewWindowState);

			if (args.Event.NewWindowState == Gdk.WindowState.Iconified) {
				iconified = true;
				gfax.MainWindow.SkipTaskbarHint = true;	
			} else {
				gfax.MainWindow.SkipTaskbarHint = false;				
				iconified = false;
			}

		}
		//************************************************************************
		// queue_refresh
		//		returns true to continue timeout functioning
		//	
		// Queue_refresh connects to the hylafax server and retrieves
		// updated queue information on regular intervals.  This is called
		// from a GLib.TimeoutHandler
		public bool queue_refresh ()
		{
			if (!Settings.RefreshQueueEnabled)
				return false;
			
			// always do main status			
			//update_status(Fax.get_server_status());
			
			// TODO do all this stuff with Delegates and events
			async_update_status();
			while (gfax.activeNetwork)
				System.Threading.Thread.Sleep(100);
			// now do queue

			switch (activeQ) {
				case ActiveQ.done :
					async_update_queue_status("doneq");
					break;
				case ActiveQ.send :
					async_update_queue_status("sendq");
					break;
				case ActiveQ.receive :
					async_update_queue_status("recvq");
					break;
			}

			return true;	// must return true from timeout to keep it active
		}
		
		// Set to true if we need to restart for changed transport agent
		public bool Restart
		{
			get { return restart; }
			set { restart = value; }
		}
		

//========================= PRIVATE METHODS ====================================
		private void on_StatusList_selection (object o, EventArgs args)
		{
			if (StatusList.Selection.CountSelectedRows() > 0) {
				DeleteJobButton.Sensitive = true;
				ModifyJobButton.Sensitive = true;
			} else {
				DeleteJobButton.Sensitive = false;
				ModifyJobButton.Sensitive = false;
			}
		}
		

		private void on_JobsCompleteList_selection (object o, EventArgs args)
		{
			DeleteJobButton.Sensitive = true;
		}

		private void on_JobsReceivedList_selection (object o, EventArgs args)
		{
			if (JobsReceivedList.Selection.CountSelectedRows() > 0) {
				ViewPrintButton.Sensitive = true;
				RecvfaxDeleteButton.Sensitive = true;
			} else {
				ViewPrintButton.Sensitive = false;
				RecvfaxDeleteButton.Sensitive = false;
			}
		}

		// Double click (Send tab)
		private void on_StatusList_row_activated (object o, RowActivatedArgs args)
		{
			modify_job(lv.GetSelections(ALL_COLUMNS), "sendq");
		}
		
		
		private void on_JobsCompleteList_row_activated (object o, RowActivatedArgs args)
		{
			//modify_job(jobsCompletedView.GetSelections(ALL_COLUMNS), "doneq");
		}

		
		private void on_JobsReceivedList_row_activated (object o, RowActivatedArgs args)
		{
			// get the selected jobs
			ArrayList al;
			string hfaxfile = null;
			
			// COLUMN_5 is the filename
			al = jobsReceivedView.GetSelections(COLUMN_5);
			
			IEnumerator enu = al.GetEnumerator();
			while (	enu.MoveNext() ) {
				hfaxfile = (string)enu.Current;
			}
			//Console.WriteLine("File to receive is {0}", hfaxfile);
			
			view_received_fax( hfaxfile );
		}

		private void on_exitButton_clicked (object o, EventArgs args)
		{
			Application.Quit();
		}

		private void on_mainWindow_delete_event (object o, DeleteEventArgs args) 
		{
			Application.Quit();
			args.RetVal = true;
		}

		private void on_exit1_activate (object o, EventArgs args)
		{
			Application.Quit();
			restart = true;
		}


//Status list notebook signal, switch pages to show different queues
		private void on_StatusNotebook_switch_page (object o, SwitchPageArgs args)
		{
			async_update_status();
			while (gfax.activeNetwork)
				System.Threading.Thread.Sleep(100);

			switch (StatusNotebook.CurrentPage) {
				case SENDQ_PAGE:
							activeQ = ActiveQ.send;
							//if (update_queue_status("sendq") > 0)
							//	DeleteJobButton.Sensitive = true;
							//else {
							DeleteJobButton.Sensitive = false;
							//}
							async_update_queue_status("sendq");
							break;
				case DONEQ_PAGE:
							DeleteJobButton.Sensitive = false;
							activeQ = ActiveQ.done;
							async_update_queue_status("doneq");
							break;
				case RECEIVEQ_PAGE:
							DeleteJobButton.Sensitive = false;
							RecvfaxDeleteButton.Sensitive = false;
							activeQ = ActiveQ.receive;
							async_update_queue_status("recvq");
							break;
			}
			
		}

//Main toolbar buttons
//=======================================================================
		private void on_NewFaxButton_clicked (object o, EventArgs args)
		{
			send_new_fax();
		}
		

		private void on_PhonebookButton_clicked (object o, EventArgs args)
		{
			GfaxPhonebook gpb = new GfaxPhonebook();
		}


		private void on_ModifyJobButton_clicked (object o, EventArgs args)
		{
			 modify_job(lv.GetSelections(ALL_COLUMNS), "sendq");
		}
		
		private void modify_job (ArrayList al, string id)
		{
		
			string jobid = null;
			string number = null;
			string status = null;
			string user = null;
			string pages = null;
			string dials = null;
			object sendat = null;
			string error = null;
					
			IEnumerator enu = al.GetEnumerator();
			while (	enu.MoveNext() ) {
				enu.MoveNext();
				jobid = (string)enu.Current;
				enu.MoveNext();
				number = (string)enu.Current;
				enu.MoveNext();
				status = (string)enu.Current;
				enu.MoveNext();
				user = (string)enu.Current;
				enu.MoveNext();
				pages = (string)enu.Current;
				enu.MoveNext();
				int idx = ((string)enu.Current).LastIndexOf(':');
				dials = ((string)enu.Current).Substring(idx + 1);
				enu.MoveNext();
				sendat = (object)enu.Current;
				enu.MoveNext();
				error = (string)enu.Current;
			}
			
			#if DEBUG
				Console.WriteLine("[ModifyJob] Date is {0}", sendat);
			#endif
			
			Glade.XML xml = new Glade.XML (null, "gfax.glade","vbox74",null);
			Dialog mjd = new Dialog();
			mjd.VBox.Add(xml.GetWidget("vbox74"));
			Gtk.Entry mje = (Gtk.Entry)xml.GetWidget("ModifyJobNumberEntry");
			Gnome.DateEdit mjde = (Gnome.DateEdit)xml.GetWidget("ModifyJobDate");
			Gtk.SpinButton mjmd = (Gtk.SpinButton)xml.GetWidget("MaxDialsSpinbutton");
			
			mjd.AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel);
			mjd.AddButton (Gtk.Stock.Ok, Gtk.ResponseType.Ok);
			
			// this is to re-enable the entry for editing so it won't be selected 
			// to begin with???  Maybe something to do with re-parenting or something.
			mje.FocusInEvent += 
					new FocusInEventHandler (on_ModifyJobNumberEntry_focus_in_event);
			
			mje.IsEditable = false;
			mje.Text = number.Trim();
			
			mjde.Time = sendat == null ? new DateTime(): (DateTime)sendat;

			mjmd.Value = Convert.ToDouble(dials.Trim());
						
			ResponseType result = (ResponseType)mjd.Run ();

			if (result == ResponseType.Ok) {
				DateTime newsend = (mjde.Time).ToUniversalTime();
				
				// Format time to send - timezone is in UTC format.
				string tts = String.Format("{0}{1:00}{2:00}{3:00}{4:00}",
					newsend.Year,
					newsend.Month,
					newsend.Day,
					newsend.Hour,
					newsend.Minute);
				
				if (id == "sendq" )
					Fax.modify_job(jobid, mje.Text, tts, (mjmd.ValueAsInt).ToString());
				else // "doneq"
					Fax.resubmit_job(jobid, mje.Text, tts, (mjmd.ValueAsInt).ToString());
			
				mjd.Destroy();
			} else {
				mjd.Destroy();
			}
			
			async_update_queue_status("sendq");
			ModifyJobButton.Sensitive = false;
			DeleteJobButton.Sensitive = false;
		}
		
		public void on_ModifyJobNumberEntry_focus_in_event (object o, EventArgs args)
		{
			Gtk.Entry e = o as Gtk.Entry;
			e.IsEditable = true;
		}
		
		private void on_DeleteJobButton_clicked (object o, EventArgs args)
		{
			// get the selected jobs
			ArrayList al;
			
			// get a list of jobids to kill
			al = lv.GetSelections(COLUMN_1);
			
			IEnumerator enu = al.GetEnumerator();
			while (	enu.MoveNext() ) {
				Fax.delete_job((string)enu.Current);
			}
			
			switch (activeQ) {
					case ActiveQ.done :
						async_update_queue_status("doneq");
						break;
					case ActiveQ.send :
						async_update_queue_status("sendq");
						break;
					case ActiveQ.receive :
						async_update_queue_status("recvq");
						break;
			}
			
			DeleteJobButton.Sensitive = false;
			ModifyJobButton.Sensitive = false;
		}
		
		private void on_ViewPrintButton_clicked (object o, EventArgs args)
		{
			// get the selected jobs
			ArrayList al;
			string hfaxfile = null;
			
			// COLUMN_5 is the filename
			al = jobsReceivedView.GetSelections(COLUMN_5);
			
			IEnumerator enu = al.GetEnumerator();
			while (	enu.MoveNext() ) {
				hfaxfile = (string)enu.Current;
			}
			
			view_received_fax (hfaxfile);
		}
		
		private void view_received_fax (string s)
		{
			// TODO get proper Gnome mime type and use that first if running Gnome
			try {
				if (Fax.recvfax(s)) {
					ProcessStartInfo pidInfo = new ProcessStartInfo();
					pidInfo.FileName = Settings.FaxViewer;
					pidInfo.Arguments = String.Concat(gfax.SpoolDirectory, "/tif/", s);
					System.Diagnostics.Process pid = System.Diagnostics.Process.Start(pidInfo);
				} else {
					G_Message gm = new G_Message(Catalog.GetString(
@"Cannot read the facsimile file from the server. You most likely do not 
have permission to read the file.  Check the settings on your fax server." ));
					return;
				} 
				
			} catch (Exception e) {
        Console.WriteLine("[gui.onViewPrintButton_clicked] Exception: {0}", e);
				G_Message gm = new G_Message(Catalog.GetString(
@"Cannot start your faxviewer program. 
Please check your settings.  It is currently set to " + Settings.FaxViewer + "." ));
			}
			
			switch (activeQ) {
					case ActiveQ.done :
						async_update_queue_status("doneq");
						break;
					case ActiveQ.send :
						async_update_queue_status("sendq");
						break;
					case ActiveQ.receive :
						async_update_queue_status("recvq");
						break;
			}
			
			// Reset progressbar
			gfax.MainProgressBar.Fraction = 0;
		}

		private void on_RecvfaxDeleteButton_clicked (object o, EventArgs args)
		{
			// get the selected jobs
			ArrayList al;
			string hfaxfile = null;
			
			// COLUMN_5 is the filename
			al = jobsReceivedView.GetSelections(COLUMN_5);
			
			IEnumerator enu = al.GetEnumerator();
			while (	enu.MoveNext() ) {
				hfaxfile = (string)enu.Current;

				try {
					if (Fax.delete_file(hfaxfile) == 1) {
						G_Message gm = new G_Message(Catalog.GetString(
@"Cannot delete the facsimile file from the server. You most likely do not 
have permission to delete the file.  Check the settings on your fax server." ));
						return;
					}
				} catch (Exception e) {
          Console.WriteLine("[gui.on_RecvfaxDeleteButton_clicked] Exception: {0}", e);
					G_Message gm = new G_Message(Catalog.GetString(
@"Cannot delete the file on the Hylafax server. 
Please check your settings or contact your system Administrator"));
				}
			
				switch (activeQ) {
					case ActiveQ.done :
						async_update_queue_status("doneq");
						break;
					case ActiveQ.send :
						async_update_queue_status("sendq");
						break;
					case ActiveQ.receive :
						async_update_queue_status("recvq");
						break;
				}
			}
		}



// Menu items selected =============================================
		private void on_newfax_activate (object o, EventArgs args)
		{
			send_new_fax();	
		}
		
		private void on_preferences_activate (object o, EventArgs args)
		{	
			GfaxPrefs gp = new GfaxPrefs();
		}

		private void on_jobs1_activate (object o, EventArgs args)
		{

		}

		private void on_auto_queue_refresh_check_menu_item_activate (object o, EventArgs args)
		{
			if (eventsEnabled) {
				Settings.RefreshQueueEnabled = !Settings.RefreshQueueEnabled;
				if (Settings.RefreshQueueEnabled)
					GLib.Timeout.Add((uint)(Settings.RefreshQueueInterval * 1000), 
						new TimeoutHandler(queue_refresh));
			}
		}

		private void on_log_enabled_check_menu_item_activate (object o, EventArgs args)
		{
			if (eventsEnabled)
				Settings.LogEnabled = !Settings.LogEnabled;
		}
		
		private void on_email_notification_check_menu_item_activate (object o, EventArgs args)
		{
			if (eventsEnabled)
				Settings.EmailNotify = !Settings.EmailNotify;
		}

		private void on_hi_resolution_mode_check_menu_item_activate (object o, EventArgs args)
		{
			if (eventsEnabled)
				Settings.HiResolution = !Settings.HiResolution;
		}
		
		private void on_fax_tracing_check_menuItem_activate (object o, EventArgs args)
		{
			if (eventsEnabled)
				Settings.Faxtracing = !Settings.Faxtracing;
		}

		
		private static AboutDialog about = null;
		private static void on_about_activate (object sender, EventArgs args)
		{
			if (about == null)
				about = new AboutDialog();
			
			about.ProgramName = NAME;
			about.Version = VERSION;
			about.LogoIconName = "gfax";
			about.Authors = new string[] { "George Farris <farrisg@gmsys.com>" };
			about.Documenters = new string[] {};
			about.Copyright = Catalog.GetString("Copyright (C) 2003 George Farris <farrisg@gmsys.com>");
			about.Comments = Catalog.GetString("A Facsimile application for GNOME");
			about.TranslatorCredits = (
					"Johannes Rohr <johannes@rohr.org> - German\n" +
					"Maris Dembovskis <marisdembovskis@gmail.com> - Latvian\n" +
					"Sasa Ostrouska <saxa@droplinegnome.org> Italian\n" +
					"Bart Verstraete <bartverstraete@telenet.be> - Dutch");
			about.Run();
			about.Hide();
		}

		
//--------------------------- SUPPORT FUNCTIONS -------------------------------
		Gdk.Pixbuf fax_fail;
		Gdk.Pixbuf fax_sent;
		Gdk.Pixbuf fax_sleep;
		Gdk.Pixbuf fax_block;
		Gdk.Pixbuf fax_run;
		Gdk.Pixbuf fax_receiving;
		Gdk.Pixbuf fax_received;

		public void InitListViewIcons()
		{
			fax_receiving = Gtk.IconTheme.Default.LoadIcon ("document-save", 16, 0);
			fax_fail = Gtk.IconTheme.Default.LoadIcon ("gtk-cancel", 16, 0);
			fax_sent = Gtk.IconTheme.Default.LoadIcon ("gtk-apply", 16, 0);
			fax_run = Gtk.IconTheme.Default.LoadIcon ("printer-printing", 16, 0);
			fax_block = Gtk.IconTheme.Default.LoadIcon ("printer-error", 16, 0);
			fax_sleep = Gtk.IconTheme.Default.LoadIcon ("appointment-soon", 16, 0);
			fax_received = Gtk.IconTheme.Default.LoadIcon ("text-x-generic", 16, 0);
		}

		public Gdk.Pixbuf GetFaxStatusIcon (FaxStatus status)
		{
			switch (status)
			{
				case FaxStatus.Fail:
					return fax_fail;
				case FaxStatus.New:
					return fax_run;
				case FaxStatus.Busy:
					return fax_block;
				case FaxStatus.Block:
					return fax_block;
				case FaxStatus.Sleep:
					return fax_sleep;
				case FaxStatus.Run:
					return fax_run;
				case FaxStatus.Receiving:
					return fax_receiving;
				case FaxStatus.Received:
					return fax_received;
			}

			return fax_sent;
		}
		
		// Updates the status text widget
		// only used for Efax
		public void update_status (string s)
		{
			if (s == null)
				return;
			
			if (Settings.TransmitAgent == "efax") {
				TextMark tm = StatusTextBuffer.GetMark("insert");				
				StatusTextBuffer.InsertAtCursor(String.Concat(s, "\n"));
				StatusText.ScrollMarkOnscreen(tm);
			}
		}

		public void async_update_status ()
		{
			string s;
			
			if (Settings.TransmitAgent == "efax") {
				s = Fax.async_get_server_status();
				if (s != null ) {
					TextMark tm = StatusTextBuffer.GetMark("insert");				
					StatusTextBuffer.InsertAtCursor(String.Concat(s, "\n"));
					StatusText.ScrollMarkOnscreen(tm);
				}
			} 
			
			if (Settings.TransmitAgent == "hylafax") {
				// Progressbar pulser - add this when we have async comms
				GLib.Timeout.Add((uint)(100), new TimeoutHandler (queue_progress));

				// update status bar
				Appbar.ClearStack();
				Appbar.Push(Catalog.GetString("Refreshing server status..."));
				Appbar.Refresh();
				//GLib.MainContext.Iteration ();
				while (Gtk.Application.EventsPending ())
					Gtk.Application.RunIteration ();

				s = Fax.async_get_server_status();
			}
		}


		private void async_update_queue_status(string queue)
		{
			G_ListView view;
			Fax.FaxQueue q = null;
			Fax.FaxRecQueue rq = null;
						
			view = lv;
				
			switch (queue) {
				case "sendq":
						view = lv;
						break;
				case "doneq":
						view = jobsCompletedView;
						break;
				case "recvq":
						view = jobsReceivedView;
						break;
			}
			
			if (Settings.TransmitAgent == "hylafax") {
				// Progressbar pulser - add this when we have async comms
				GLib.Timeout.Add((uint)(100), new TimeoutHandler (queue_progress));

				// update status bar
				Appbar.ClearStack();
				Appbar.Push(Catalog.GetString("Refreshing queue..."));
				Appbar.Refresh();
				//GLib.MainContext.Iteration ();
				while (Gtk.Application.EventsPending ())
					Gtk.Application.RunIteration ();
					
				Fax.async_get_queue_status(queue);
			}
			if (Settings.TransmitAgent == "efax") {
				async_update_listview(Fax.async_get_queue_status(queue), queue);
			}
		}

		// We end up here when an async network read is finished
		private void async_net_read_finished ()
		{
			switch (gfax.asyncReadType) {
				case "doneq" :
					async_update_listview(Fax.parse_senddone(gfax.asyncFolderMessageBuffer), "doneq");
					break;
				case "sendq" :
					async_update_listview(Fax.parse_senddone(gfax.asyncFolderMessageBuffer), "sendq");
					break;
				case "recvq" :
					async_update_listview(Fax.parse_receive(gfax.asyncFolderMessageBuffer), "recvq");
					break;
				case "status" :
					StatusTextBuffer.Text = gfax.asyncServerMessageBuffer;
					break;
			}
		}

		private void async_update_listview(ArrayList reply, string queue)
		{
			G_ListView view;
			Fax.FaxQueue q = null;
			Fax.FaxRecQueue rq = null;
						
			view = lv;
				
			switch (queue) {
				case "sendq":
						view = lv;
						break;
				case "doneq":
						view = jobsCompletedView;
						break;
				case "recvq":
						view = jobsReceivedView;
						break;
			}
			
			if (reply.Count > 0) {
				StatusStore.Clear();
				RecvStore.Clear();
				IEnumerator enu = reply.GetEnumerator();
				
				if (queue == "sendq" || queue == "doneq") {
					while ( enu.MoveNext() ) {
   	     				q = (Fax.FaxQueue)enu.Current;
						view.AddTextToRow(GetFaxStatusIcon(q.StatusType), q.Jobid, q.Number, q.Status, q.Owner, q.Pages, q.Dials, q.Sendat, q.Error);
					}	
					
					Appbar.ClearStack();
						
					if (q.Jobid != "") {
						Appbar.Push(Catalog.GetString("There are " + reply.Count + " jobs in the queue"));
						Appbar.Refresh();
						//((Gtk.Window) gxml["Gfax"]).Title = "Gfax (" + reply.Count + ")";
						GfaxWindow.Title = "Gfax (" + reply.Count + ")";
						//return reply.Count;
						return;
					} else {
						Appbar.Push(Catalog.GetString("There are 0 jobs in the queue"));
						Appbar.Refresh();
						//((Gtk.Window) gxml["Gfax"]).Title = "Gfax";
						GfaxWindow.Title = "Gfax";
					}
				} else {  //receive queue
					while ( enu.MoveNext() ) {
   	     				rq = (Fax.FaxRecQueue)enu.Current;
						view.AddTextToRow(GetFaxStatusIcon(rq.StatusType), rq.Sender, rq.Status, rq.Pages, rq.TimeReceived, rq.Filename);
					}
					
					if (rq.Sender != "") {
						Appbar.Push(Catalog.GetString("There are " + reply.Count + " jobs in the queue"));
						Appbar.Refresh();
						//((Gtk.Window) gxml["Gfax"]).Title = "Gfax (" + reply.Count + ")";
						GfaxWindow.Title = "Gfax (" + reply.Count + ")";
						//return reply.Count;
						return;
					} else {
						Appbar.Push(Catalog.GetString("There are 0 jobs in the queue"));
						Appbar.Refresh();
						//((Gtk.Window) gxml["Gfax"]).Title = "Gfax";
						GfaxWindow.Title = "Gfax";
					}
				}
				
				oldSendQueue = reply;	// else save queue
			}


			StatusStore.Clear();			
			RecvStore.Clear();
		}


		private bool queue_progress ()
		{
			if (gfax.activeNetwork) {
				Appbar.Progress.Pulse();
				return (true);
			}
			else {
				async_net_read_finished();
				Appbar.Progress.Fraction = 0;
				return (false);
			}
		}

		// This is where we end up if the New Fax button or the menu item 
		// has been selected.
		private void send_new_fax ()
		{
				
			string [] largs = {"do_filename"};
			GfaxSend sd = new GfaxSend ("", largs);
			
			// send the faxes
			if (sd.DoSend) {
				Fax.sendfax(sd.Filename);
				// if file is temp data (/var/spool/gfax/D.*) then delete it
				FileInfo f = new FileInfo(sd.Filename);
				if (File.Exists(String.Concat(gfax.SpoolDirectory, "/", f.Name)))
					File.Delete(String.Concat(gfax.SpoolDirectory, "/", f.Name));
			}
			activeQ = ActiveQ.send;
			async_update_queue_status("sendq");
			sd = null;
			largs = null;
		}

	}

	public class Status
	{
		TextBuffer stb;
		TextView stv;
		
		public Status (Gtk.TextView StatusText, TextBuffer StatusTextBuffer)
		{
			stv = StatusText;
			stb = StatusTextBuffer;
		}
		
		public void Append (string s)
		{
			TextMark tm = stb.GetMark("insert");				
			stb.InsertAtCursor(String.Concat(s, "\n"));
			stv.ScrollMarkOnscreen(tm);
		}
	}
	
	// pulse the main window progressbar
	public class Pulser
	{
		public bool endPulse;
		
		public void StartPulse ()
		{
			endPulse = true;
			GLib.Timeout.Add((uint)(200), 
				new TimeoutHandler (PulseIt));
		}
		
		public void EndPulse ()
		{
			endPulse = false;
		}
		
		private bool PulseIt ()
		{
			gfax.MainProgressBar.Pulse();
			if (endPulse)
				return(true);
			else {
			gfax.MainProgressBar.Fraction = 0;
				return(false);
			}
		}
	}

}
