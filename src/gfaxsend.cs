// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA 02111-1307, USA.

//************************************************************************
// GfaxSend class
//
// This is a the send window for sending of a facsimile.
namespace gfax {
	using Mono.Unix;
	using System;
	using System.IO;
	using GLib;
	using Gtk;
	using Gnome;
	using Glade;
	using GConf.PropertyEditors;
	using GtkSharp;
	using System.Runtime.InteropServices;
	using System.Collections;
	using System.Diagnostics;
	using System.Reflection;
	
	
	public class GfaxSend // : Program
	{
		const string APPNAME = "Gfax";
	
		const int COLUMN_0 = 0;
		const int COLUMN_1 = 1;
		const int COLUMN_2 = 2;
		const int COLUMN_3 = 3;
		const int ALL_COLUMNS = -1;	
	
		[Glade.Widget] Gtk.Window NewFaxDialog;
		[Glade.Widget] Gtk.Button BrowseButton;		
		[Glade.Widget] Gtk.Button PhoneButton;
		[Glade.Widget] Gtk.Entry NumberEntry;
		[Glade.Widget] Gtk.Entry EmailEntry;
		[Glade.Widget] Gtk.Entry FilenameEntry;
		[Glade.Widget] Gtk.CheckButton ResolutionCheckbutton;
		[Glade.Widget] Gtk.CheckButton EmailCheckbutton;
		[Glade.Widget] Gtk.CheckButton SendCheckbutton;
		[Glade.Widget] Gtk.TreeView ItemTreeview;
		[Glade.Widget] Gnome.DateEdit SendDateedit;
		//[Glade.Widget] Gnome.FileEntry PSFileEntry;

	
		Gtk.ListStore ItemStore;
		G_ListView ItemListview;
		
		GConf.PropertyEditors.EditorShell shell;		
		
		Glade.XML gxml;
		string filename, includeFilename;
		bool dosend;
		
		public GfaxSend (string fname, string[] args)
		{
			
			filename = fname;
			includeFilename = args[0];
			
			Application.Init ();
			
			if (Settings.Faxtracing == true) {
				Console.WriteLine("[GfaxSend] File name is : {0}", fname);
			}
			
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
Please run Gfax from the menu or command line and set your 
MODEM TYPE, PORT and Fax number under preferences.

Gfax is initially setup to use Efax, you may change it use 
Hylafax if you prefer or require connection to a network 
facsimile server.")
				);
				md.Run ();
				md.Destroy();
				Application.Quit();
				dosend = false;
				Environment.Exit(0);
			}
/*
			if ( Settings.FaxNumber.Length <= 4 ) {
				MessageDialog md;
				md = new MessageDialog (
					null,
					DialogFlags.DestroyWithParent, 
					MessageType.Info, 
					ButtonsType.Ok,
					Catalog.GetString(
						@"
You must have a Fax number set in your preferences under
Phone Information.")
				);
				md.Run ();
				md.Destroy();
				//Application.Quit();
				dosend = false;
				return;
			}
*/
			gxml = new Glade.XML (null, "send-druid.glade","NewFaxDialog",null);
			gxml.Autoconnect (this);
						
			// Set the program icon
			//Gdk.Pixbuf Icon = new Gdk.Pixbuf(null, "gfax.png");
			//((Gtk.Window) gxml["NewFaxDialog"]).Icon = Icon;
			//NewFaxDialog.Icon = Icon;
			
			ItemStore = new ListStore(
					typeof (Boolean),
					typeof (string),
					typeof (string),
					typeof (string));
			
			ItemListview = new G_ListView(ItemTreeview, ItemStore);
			
			ItemListview.AddColumnTitleToggle(Catalog.GetString("Send"), 0, COLUMN_0);
			ItemListview.AddColumnTitle(Catalog.GetString("Phone Number"), 0, COLUMN_1);
			ItemListview.AddColumnTitle(Catalog.GetString("Organization"), 0, COLUMN_2);
			ItemListview.AddColumnTitle(Catalog.GetString("Contact"), 0, COLUMN_3);
			
			ItemTreeview.HeadersVisible = true;
	
			//ItemTreeview.Selection.Mode = SelectionMode.Multiple;
	
			ResolutionCheckbutton.Active = Settings.HiResolution;
			EmailCheckbutton.Active = Settings.EmailNotify;
			EmailEntry.Text = Settings.EmailAddress;

			if (Settings.TransmitAgent == "efax") {
				EmailCheckbutton.Visible = false;
				EmailEntry.Visible = false;
				SendCheckbutton.Visible = false;
				SendDateedit.Visible = false;
			}
	
			if (SendCheckbutton.Active)
				SendDateedit.Sensitive = false;
	
			// If we have a file name from the gnome print dialog
			if (includeFilename != "do_filename") {
				FilenameEntry.Text = Catalog.GetString("Spooled from print job");
				FilenameEntry.Sensitive = false;
				BrowseButton.Sensitive = false;
			}
	
			Application.Run ();
		}

		public bool DoSend
		{
			get { return dosend; }
		}
	
		public string Filename
		{
			get { return filename; }
		}
	
	
		private void on_window1_delete_event (object o, DeleteEventArgs args) 
		{
			dosend = false;		
			//((Gtk.Window) gxml["NewFaxDialog"]).Destroy();			
			NewFaxDialog.Destroy();
			Application.Quit ();
			args.RetVal = true;
		}
		
		private void on_CancelButton_clicked (object o, EventArgs args) 
		{
			dosend = false;
			//((Gtk.Window) gxml["NewFaxDialog"]).Destroy();
			NewFaxDialog.Destroy();
			Application.Quit ();
		}
	
		private void on_NumberEntry_changed (object o, EventArgs args) 
		{
			//if (NumberEntry.Text == "\t")
				//((Gnome.Druid) gxml["send_druid"]).HasFocus = true;
		}
		
		private void on_SendCheckButton_toggled (object o, EventArgs args)
		{
		SendDateedit.Sensitive = ! SendDateedit.Sensitive;
		}
		
		private void on_NumberEntry_activate (object o, EventArgs args) 
		{
			Gtk.TreeIter iter = new Gtk.TreeIter();
			
			iter = ItemStore.AppendValues(true, NumberEntry.Text, "", "");
			ItemTreeview.Model = ItemStore;
			NumberEntry.Text = "";
		}
		
		
		private void  on_BrowseButton_clicked (object o, EventArgs args) 
		{
			FileChooserDialog fc = new FileChooserDialog("Choose postscript file", null, FileChooserAction.Open);
			fc.AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel);
			fc.AddButton (Gtk.Stock.Open, Gtk.ResponseType.Ok);
			
			ResponseType result = (ResponseType)fc.Run ();
	
			if (result == ResponseType.Ok) {
				FilenameEntry.Text = fc.Filename;
				fc.Destroy();
			} else {
				fc.Destroy();
			}
		}
	
	
		private void  on_PhoneButton_clicked (object o, EventArgs args) 
		{
			Gtk.TreeIter iter = new Gtk.TreeIter();
			// display the phone book, when we return gfax.Destinations 
			// will be set with contacts.
			GfaxSendPhoneBook gfs = new GfaxSendPhoneBook(gxml, "gfaxsend");
			
			ItemStore.Clear();
	
			if (gfax.Destinations.Count > 0) {
				IEnumerator enu = gfax.Destinations.GetEnumerator();
		  		
				while ( enu.MoveNext() ) {
					iter = ItemStore.AppendValues(true,
							((GfaxContact)enu.Current).PhoneNumber,
							((GfaxContact)enu.Current).Organization,
							((GfaxContact)enu.Current).ContactPerson);
					ItemTreeview.Model = ItemStore;
				}
			}
		}
	
	
		// When finished gfax.Destinations will contain a list of contacts
		// of Phonebook.Contact type.
		private void on_SendfaxButton_clicked (object o, EventArgs args) 
		{	
			Gtk.TreeIter iter = new Gtk.TreeIter();
			ArrayList rows = new ArrayList();
			
			if (includeFilename == "do_filename")
				filename = FilenameEntry.Text;
				
			if (!File.Exists(filename)) {
				MessageDialog md;
				md = new MessageDialog (null, DialogFlags.DestroyWithParent, 
					MessageType.Info, ButtonsType.Ok,
					Catalog.GetString(
						@"
The file you have entered does not exist.
Please check the name and try again.")
				);
				md.Run ();
				md.Destroy();
				return;
			}
				
			// clear all the distinations, it's a little wierd yup
			gfax.Destinations.Clear();
			
			// Get the first row.
			ItemStore.GetIterFirst(out iter);
	
			try {
				if ( (bool)ItemStore.GetValue(iter, 0) ) {		// if send is true (toggle set)
										
					GfaxContact c = new GfaxContact();
					c.PhoneNumber = (string)ItemStore.GetValue(iter, 1);	// number
					c.Organization = (string)ItemStore.GetValue(iter, 2);	// organization
					c.ContactPerson = (string)ItemStore.GetValue(iter, 3);	// contact
									
					rows.Add(c);
				}
			}
			catch (Exception e) {
			  Console.WriteLine("[guitools.on_faxsendbutton_clicked] Exception: {0}", e);
				MessageDialog md;
				md = new MessageDialog (
					null,
					DialogFlags.DestroyWithParent, 
					MessageType.Info, 
					ButtonsType.Ok,
					Catalog.GetString(
						@"
You have not entered a facsimile number!
Please enter a number and press the <i><b>Enter</b></i> key or
click the <i><b>Phone Book</b></i> button to select numbers or
entire phone books.")
				);
				md.Run ();
				md.Destroy();
				return;
			}
			
			// get the rest of the rows			
			while (ItemStore.IterNext(ref iter)) {
	
				try {
					if ( (bool)ItemStore.GetValue(iter, 0) ) {		// if send is true (toggle set)
										
						GfaxContact c = new GfaxContact();
						c.PhoneNumber = (string)ItemStore.GetValue(iter, 1);	// number
						c.Organization = (string)ItemStore.GetValue(iter, 2);	// organization
						c.ContactPerson = (string)ItemStore.GetValue(iter, 3);	// contact
										
						rows.Add(c);
					}
				}
				catch (Exception e) {
				  Console.WriteLine("[guitools.onfaxsendbutton_clicked] Exception: {0}", e);
				}
			}
	
						
			if (SendCheckbutton.Active) {
				gfax.timeToSend = DateTime.UtcNow;
			} else {
				// Convert to UTC for Hylafax
				gfax.timeToSend = (SendDateedit.Time).ToUniversalTime();
			}
			
			gfax.Destinations = rows;
			
			//get the fine resolution status
			gfax.sendWizardResolution = ResolutionCheckbutton.Active;
			//get the email flag and email address
			gfax.sendWizardEmailNotify = EmailCheckbutton.Active;
			gfax.sendWizardEmailAddress = EmailEntry.Text;				
		
			if ( gfax.Destinations.Count > 0 ) 
				dosend = true;	// yes send the fax
				
			//((Gtk.Window) gxml["NewFaxDialog"]).Hide();
			NewFaxDialog.Hide();
			Application.Quit();
		}
	}
}
