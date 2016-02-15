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

	//************************************************************************
	// GfaxPhonebook class
	//
	
	public class GfaxPhonebook
	{
		const string APPNAME = "Gfax";
		
		const int COLUMN_0 = 0;
		const int COLUMN_1 = 1;
		const int COLUMN_2 = 2;
		const int COLUMN_3 = 3;
		const int ALL_COLUMNS = -1;
		
		string tempName;
		string tempNumber;
		string tempCompany;
		
		bool eventsEnabled = true;
		
		[Glade.Widget] Gtk.Window PhbookWindow;
		[Glade.Widget] Gtk.ComboBox PhonebookComboBox;
		[Glade.Widget] Gtk.TreeView ItemTreeview;
		[Glade.Widget] Gtk.Entry PhonebookComboEntry;
		[Glade.Widget] Gtk.Entry EditPhbNumberEntry;
		[Glade.Widget] Gtk.Entry EditPhbNameEntry;
		[Glade.Widget] Gtk.Entry EditPhbCompanyEntry;
		[Glade.Widget] Gtk.ToolButton SaveCloseButton;
		[Glade.Widget] Gtk.ToolButton DeletePhonebookButton;
		[Glade.Widget] Gtk.Button AddButton;
		[Glade.Widget] Gtk.Button UpdateButton;		
		[Glade.Widget] Gtk.Button ClearButton;
		[Glade.Widget] Gtk.Statusbar Statusbar;
		
		Gtk.ListStore ItemStore;	//phonebook item store
		G_ListView ItemView;		
		
		GConf.PropertyEditors.EditorShell shell;		
		Phonebook[] myPhoneBooks;		
		Glade.XML gxml;
		const int id = 1;
		bool pbIsReadOnly = false;		//wether current phone book is no editable.
		
		public GfaxPhonebook ()
		{
			myPhoneBooks = Phonetools.get_phonebooks();
						
			Application.Init ();
			gxml = new Glade.XML (null, "send-druid.glade","PhbookWindow",null);
			gxml.Autoconnect (this);
		
			ItemStore = new ListStore(typeof (string), typeof (string), typeof (string));
			ItemView = new G_ListView(ItemTreeview, ItemStore);
			ItemView.AddColumnTitle(Catalog.GetString("Organization"), 0, COLUMN_0);
			ItemView.AddColumnTitle(Catalog.GetString("Phone Number"), 1, COLUMN_1);
			ItemView.AddColumnTitle(Catalog.GetString("Contact"), 2, COLUMN_2);
			ItemTreeview.HeadersVisible = true;
			//ItemTreeview.Selection.Mode = SelectionMode.Multiple;
			ItemTreeview.Selection.Changed += 
					new EventHandler(on_ItemTreeview_selection);
			
			// Populate the drop down combo box with phonebooks and populate
			// the list with the first phonebook.
			// TODO sort these alphabetically
			if ( myPhoneBooks.Length > 0) {
				string[] list = new string[myPhoneBooks.Length];
				
				if ( myPhoneBooks != null ) {
					// populate the list
					int i = 0;
					PhonebookComboBox.RemoveText(0);
					foreach (Phonebook p in myPhoneBooks) {
						list[i++] = p.Name;
						PhonebookComboBox.AppendText(p.Name);
					}
					PhonebookComboBox.Active = 0;
				}
				
				//Console.WriteLine(list[PhonebookComboBox.Active]);
				DeletePhonebookButton.Sensitive = true;
			} else {
				DeletePhonebookButton.Sensitive = false;
			}
			
			SaveCloseButton.Sensitive = false;
			UpdateButton.Sensitive = false;
			ClearButton.Sensitive = false;
			AddButton.Sensitive = false;

			Application.Run ();
		}


		private void on_PhbookWindow_delete_event (object o, DeleteEventArgs args) 
		{
			PhbookWindow.Destroy();			
			Application.Quit ();
		}
		
		// Menu signals
		private void on_New_activate (object o, EventArgs args)
		{
			on_NewPhonebookButton_clicked(null, null);
		}
		
		private void on_SaveAndClose_activate (object o, EventArgs args)
		{
			on_SaveCloseButton_clicked(null, null);
		}
		
		private void on_Delete_activate (object o, EventArgs args)
		{
			on_DeletePhonebookButton_clicked(null, null);
		}
		
		private void on_Close_activate (object o, EventArgs args)
		{
			on_CloseButton_clicked(null, null);
		}
		//--------- end of menu -----------------

		
		private void on_NewPhonebookButton_clicked (object o, EventArgs args)
		{
			// Unselect any phone book otherwise we get gtk errors.
			ItemTreeview.Selection.UnselectAll();
			
			// run the wizard
			NewPhoneBook nphb = new NewPhoneBook ();
            nphb.Run();

			if ( nphb.PhoneBookNames.Count > 0 )	{	// don't do this if cancelled
				IEnumerator enu = nphb.PhoneBookNames.GetEnumerator();
    	  	
				while ( enu.MoveNext() ) {
					Phonebook ph = new Phonebook();
					if (nphb.PhoneBookType == "gfax")
						ph.Path = gfax.ConfigDirectory + "/" + (string)enu.Current;
					else
						ph.Path = "";
					
					ph.Name = (string)enu.Current;	// get the new book name
					ph.Type = nphb.PhoneBookType;
					Phonetools.add_book(ph);
					PhonebookComboBox.AppendText(ph.Name);
				}
					// Reload the phone books
				myPhoneBooks = Phonetools.get_phonebooks();
				
				if ( myPhoneBooks.Length > 0) {
					//PhonebookComboBox.AppendText(ph.Name);
					PhonebookComboBox.Active = 0;
					DeletePhonebookButton.Sensitive = true;
				}
			}
			
			nphb = null;
        }

		private void on_DeletePhonebookButton_clicked (object o, EventArgs args)
		{

			if ( myPhoneBooks.Length > 0) {
				string[] list = new string[myPhoneBooks.Length];
				
				if ( myPhoneBooks != null ) {
					// populate the list
					int i = 0;
					foreach (Phonebook pb in myPhoneBooks) {
						list[i++] = pb.Name;
					}
				}
				string book = list[PhonebookComboBox.Active];
			
				MessageDialog md = new MessageDialog (
						null,
						DialogFlags.DestroyWithParent, 
						MessageType.Question, 
						ButtonsType.YesNo,
						Catalog.GetString("Are you sure you want to delete the phone book?")
				);
     
				ResponseType result = (ResponseType)md.Run ();

				if (result == ResponseType.Yes) {
					md.Destroy();
				
					if (book == null)
						return;
					Phonetools.delete_book(book);

					PhonebookComboBox.RemoveText(PhonebookComboBox.Active);
					ItemStore.Clear();
					
					myPhoneBooks = Phonetools.get_phonebooks();

					if (myPhoneBooks.Length > 0) {
						// now reload
						PhonebookComboBox.Active = 0;
						on_PhonebookComboBox_changed(null, null);
					} else {
						DeletePhonebookButton.Sensitive = false;
						PhonebookComboBox.InsertText(0," ");
					}
					PhonebookComboBox.Active = 0;
					
				} else {
					md.Destroy();
				}
			}
		}
	
		private void on_SaveCloseButton_clicked (object o, EventArgs args)
		{
			ArrayList rows;
			ArrayList contacts = new ArrayList();
			string book;
			
			if ( myPhoneBooks.Length > 0) {
				string[] list = new string[myPhoneBooks.Length];
				
				if ( myPhoneBooks != null ) {
					// populate the list
					int i = 0;
					foreach (Phonebook pb in myPhoneBooks)
						list[i++] = pb.Name;
				}
				book = list[PhonebookComboBox.Active];
				
				// just get all items and save
				SaveCloseButton.Sensitive = false;
				//eventsEnabled = false;
				EditPhbCompanyEntry.Text = "";
				EditPhbNumberEntry.Text = "";
				EditPhbNameEntry.Text = "";
			
				rows = ItemView.GetAllRows();
						
				IEnumerator enu = rows.GetEnumerator();
				while ( enu.MoveNext() ) {
					GfaxContact c = new GfaxContact();
					c.Organization = ((string[])enu.Current)[0];
					c.PhoneNumber = ((string[])enu.Current)[1];
					c.ContactPerson = ((string[])enu.Current)[2];
					contacts.Add(c);
				}
			
				Phonetools.save_phonebook_items(book, contacts);
				//EditPhbList.Selection.UnselectAll();
			}
			
			PhbookWindow.Destroy();			
			Application.Quit ();
		}
		
		private void on_CloseButton_clicked (object o, EventArgs args)
		{
			if (SaveCloseButton.Sensitive) {
				MessageDialog md = new MessageDialog (
					null,
					DialogFlags.DestroyWithParent, 
					MessageType.Question, 
					ButtonsType.YesNo,
					Catalog.GetString("You have unsaved phone book entries.\n Are you sure you want to Quit?")
				);
     
				ResponseType result = (ResponseType)md.Run ();

				if (result == ResponseType.Yes) {
					md.Destroy();
					PhbookWindow.Destroy();			
					Application.Quit ();
				} else {
					md.Destroy();
				}
			} else {
				PhbookWindow.Destroy();			
				Application.Quit ();
			}
		}


		private void on_PhonebookComboBox_changed (object o, EventArgs args)
		{
			ArrayList contacts = null;
			Phonebook p;
			
			// get the first book in the list and load the liststore
			if ( myPhoneBooks.Length > 0) {
				string[] list = new string[myPhoneBooks.Length];
				
				if ( myPhoneBooks != null ) {
					// populate the list
					int i = 0;
					foreach (Phonebook pb in myPhoneBooks) {
						list[i++] = pb.Name;
					}
				}
				
				p = Phonetools.get_book_from_name(list[PhonebookComboBox.Active]);
				if (p == null)
					return;
					
				// Clear the list_store
				ItemStore.Clear();
			
				contacts = Phonetools.get_contacts(p);
				
				if (contacts != null) {
					IEnumerator enu = contacts.GetEnumerator();
					while ( enu.MoveNext() ) {
						GfaxContact c = new GfaxContact();
						c = (GfaxContact)enu.Current;
						ItemView.AddTextToRow(c.Organization, c.PhoneNumber, c.ContactPerson);
					}
					if (p.Type == "evolution") {
						pbIsReadOnly = true;
						EditPhbCompanyEntry.Sensitive = false;
						EditPhbNumberEntry.Sensitive = false;
						EditPhbNameEntry.Sensitive = false;

					} else {
						pbIsReadOnly = false;
						EditPhbCompanyEntry.Sensitive = true;
						EditPhbNumberEntry.Sensitive = true;
						EditPhbNameEntry.Sensitive = true;
						eventsEnabled = true;
					}
				}
			}
		}

		private void on_EditPhbNumberEntry_changed (object o, EventArgs args)
		{
			if (eventsEnabled) {
				ArrayList al = ItemView.GetSelections(ALL_COLUMNS);
				// if single contact selected
				if (al.Count > 0) {
					UpdateButton.Sensitive = true;
				} else {
					AddButton.Sensitive = true;
					ClearButton.Sensitive = true;
				}
			}
		}
		private void on_EditPhbNameEntry_changed (object o, EventArgs args)
		{
			if (eventsEnabled) {
				ArrayList al = ItemView.GetSelections(ALL_COLUMNS);
				// if single contact selected
				if (al.Count > 0) {
					UpdateButton.Sensitive = true;
				} else {
					AddButton.Sensitive = true;
					ClearButton.Sensitive = true;
				}
			}
		}
		private void on_EditPhbCompanyEntry_changed (object o, EventArgs args)
		{
			if (eventsEnabled) {
				ArrayList al = ItemView.GetSelections(ALL_COLUMNS);
				// if single contact selected
				if (al.Count > 0) {
					UpdateButton.Sensitive = true;
				} else {
					AddButton.Sensitive = true;
					ClearButton.Sensitive = true;
				}
			}
		}
		
		private void on_ClearButton_clicked (object o, EventArgs args)
		{
			eventsEnabled = false;
			EditPhbCompanyEntry.Text = "";
			EditPhbNumberEntry.Text = "";
			EditPhbNameEntry.Text = "";
			UpdateButton.Sensitive = false;
			ItemTreeview.Selection.UnselectAll();
			ClearButton.Sensitive = false;
			AddButton.Sensitive = false;
			Statusbar.Pop(id);
			Statusbar.Push(id, " ");
		}
		
		private void on_AddButton_clicked (object o, EventArgs args)
		{
			ItemView.AddTextToRow(EditPhbCompanyEntry.Text,
					EditPhbNumberEntry.Text,
					EditPhbNameEntry.Text);
					
			eventsEnabled = false;  // no updates till selected
			EditPhbCompanyEntry.Text = "";
			EditPhbNumberEntry.Text = "";
			EditPhbNameEntry.Text = "";
			EditPhbNumberEntry.HasFocus = true;
			SaveCloseButton.Sensitive = true;
			ClearButton.Sensitive = false;
			AddButton.Sensitive = false;
			eventsEnabled = true;
		}

		private void on_UpdateButton_clicked (object o, EventArgs args)
		{
			ItemView.UpdateColumnText(tempNumber, EditPhbNumberEntry.Text, COLUMN_1);
			ItemView.UpdateColumnText(tempName, EditPhbNameEntry.Text, COLUMN_2);
			ItemView.UpdateColumnText(tempCompany, EditPhbCompanyEntry.Text, COLUMN_0);
			EditPhbCompanyEntry.Text = "";
			EditPhbNumberEntry.Text = "";
			EditPhbNameEntry.Text = "";
			UpdateButton.Sensitive = false;
			ItemTreeview.Selection.UnselectAll();
			ClearButton.Sensitive = false;
			SaveCloseButton.Sensitive = true;
			Statusbar.Pop(id);
			Statusbar.Push(id, " ");
		}
		
		private void on_ItemTreeview_selection (object o, EventArgs args)
		{	
			ArrayList al;
			
			eventsEnabled = false;  // no updates			
			if (!pbIsReadOnly) {	
				al = ItemView.GetSelections(ALL_COLUMNS);
				Statusbar.Push(id,Catalog.GetString("Press the <DELETE> key to delete an entry."));
			
			// if single contact selected
				if (al.Count == 3) {
					IEnumerator enu = al.GetEnumerator();
					enu.MoveNext();
					EditPhbCompanyEntry.Text = (string)enu.Current;
					tempCompany = (string)enu.Current;
					enu.MoveNext();
					EditPhbNumberEntry.Text = (string)enu.Current;
					tempNumber = (string)enu.Current;
					enu.MoveNext();
					EditPhbNameEntry.Text = (string)enu.Current;
					tempName = (string)enu.Current;
				}
				AddButton.Sensitive = false;
				ClearButton.Sensitive = true;
				eventsEnabled = true;
			}
		}

		private void on_ItemTreeview_key_press_event (object o, KeyPressEventArgs args)
		{
			if (args.Event.Key == Gdk.Key.Delete ) {
						
				//eventsEnabled = false;  // no updates			
				ArrayList al = ItemView.GetSelections(ALL_COLUMNS);
			
				// if single contact selected
				//if (al.Count == 3) {
					ItemView.RemoveSelectedRow();
					EditPhbCompanyEntry.Text = "";
					EditPhbNumberEntry.Text = "";
					EditPhbNameEntry.Text = "";
				//}
				AddButton.Sensitive = false;
				ClearButton.Sensitive = false;
				SaveCloseButton.Sensitive = true;
				//eventsEnabled = true;
			}
		}
	}
}
