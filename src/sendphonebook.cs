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
	using Mono.Unix;
	using System;
	using System.IO;
	using GLib;
	using Gtk;
	using Gnome;
	using Glade;
	using GtkSharp;
	using System.Runtime.InteropServices;
	using System.Collections;
	using System.Reflection;

	//************************************************************************
	// GfaxSendPhoneBook class
	//
	// 
	// 
	public class GfaxSendPhoneBook
	{
		Gtk.Dialog phbd;
		Gtk.TreeView book, list;
		//Gtk.TreeModel book_model; delete me
		Gtk.ListStore book_store, list_store;
		G_ListView bs, ls;
		Glade.XML gsxml;
		
		[Glade.Widget] Gtk.Dialog PhbookDialog;
		[Glade.Widget] Gtk.TreeView book_treeview;
		[Glade.Widget] Gtk.TreeView list_treeview;
		
		const int COLUMN_0 = 0;
		const int COLUMN_1 = 1;
		const int COLUMN_2 = 2;
		const int ALL_COLUMNS = -1;	
		//string parent; delete me
		Phonebook[] myPhoneBooks;
		
		public GfaxSendPhoneBook (Glade.XML xml, string myparent)
		{
			
			//Phonebook[] pb; delete me
			
			//gxml = xml;
			//parent = myparent; delete me
			myPhoneBooks = Phonetools.get_phonebooks();
			
			if ( myPhoneBooks == null ) {
				G_Message m = new G_Message(Catalog.GetString("You don't have any phone books yet."));
				m = null;
				return;
			}
		
			gsxml = new Glade.XML (null, "send-druid.glade","PhbookDialog",null);
			gsxml.Autoconnect (this);
			
			// Get the widget so we can manipulate it
			//phbd = (Gtk.Dialog) gsxml.GetWidget("PhbookDialog");
			//book = (Gtk.TreeView) gsxml.GetWidget("book_treeview");
			//list = (Gtk.TreeView) gsxml.GetWidget("list_treeview");
			phbd = PhbookDialog;
			book = book_treeview;
			list = list_treeview;
			
			book.Selection.Changed += new EventHandler (on_book_treeview_selection);
			
			phbd.Resizable = true;
			book_store = new ListStore(typeof(string));
			book.HeadersVisible = false;
			book.Selection.Mode = SelectionMode.Multiple;

			bs = new G_ListView(book, book_store);
			// Got have a column title or things won't show up
			bs.AddColumnTitle(Catalog.GetString("Phone books"), 0, COLUMN_0);
			
			
			list_store = new ListStore(
					typeof (string),
					typeof (string),
					typeof (string));
			
			ls = new G_ListView(list, list_store);
			ls.AddColumnTitle(Catalog.GetString("Organization"), 0, COLUMN_0);
			ls.AddColumnTitle(Catalog.GetString("Phone Number"), 1, COLUMN_1);
			ls.AddColumnTitle(Catalog.GetString("Contact"), 2, COLUMN_2);
			list.HeadersVisible = true;
			list.Selection.Mode = SelectionMode.Multiple;
			
			// populate the list
			foreach (Phonebook p in myPhoneBooks)
				bs.AddTextToRow(p.Name);

			phbd.Run();
		}
		
		// load the phone book
		// Since we have SelectionMode.Multiple turned on we have to 
		// jump through these hoops in GTK-2.0 to get a selection
		private void on_book_treeview_selection(object o, EventArgs args)
		{	
			Gtk.TreeIter iter = new Gtk.TreeIter();
            //Value value = new Value(); delete me
            string selectionText = null;
			
			book_store.GetIterFirst(out iter);
			if ( book.Selection.IterIsSelected(iter)) {
				selectionText = (string)book_store.GetValue(iter, 0);
			}
			
			while (book_store.IterNext(ref iter)) {
				if ( book.Selection.IterIsSelected(iter)) {
					selectionText = (string)book_store.GetValue(iter, 0);
				}
			}
			
			// Ok now we can finally load the phone book
			foreach (Phonebook p in myPhoneBooks)
				if (p.Name == selectionText)
					load_phone_book(p);
	
		}

		// If we double click the phonebook.
		private void on_book_treeview_row_activated(object o, RowActivatedArgs args)
		{	
			/*
			ArrayList bsdest = new ArrayList();
			ArrayList contacts = new ArrayList();
						
			bsdest = bs.GetSelections(COLUMN_0);
			if ( bsdest.Count > 0 ) {
				IEnumerator enu = bsdest.GetEnumerator();
				while ( enu.MoveNext() ) {
					foreach (Phonebook p in myPhoneBooks)
					if (p.Name == (string)enu.Current)
						contacts = Phonetools.get_contacts(p);
					
					// add contacts to global desinations
					IEnumerator enuc = contacts.GetEnumerator();
					while ( enuc.MoveNext() )
						gfax.Destinations.Add((Contact)enuc.Current);
				}
			}
			
			phbd.Destroy();
		*/
		}


		private void on_list_treeview_row_activated(object o, RowActivatedArgs args)
		{
		}

		private void on_ok_button_clicked(object o, EventArgs args)
		{
			ArrayList lsdest = new ArrayList();
			ArrayList bsdest = new ArrayList();
			ArrayList contacts = new ArrayList();
			
			lsdest = ls.GetSelections(ALL_COLUMNS);
			
			// if there are indiviual entries don't do entire phonebooks
			if (lsdest.Count > 0) {
				IEnumerator enu = lsdest.GetEnumerator();
				while ( enu.MoveNext() ) {
					GfaxContact c = new GfaxContact();
					c.Organization = (string)enu.Current;
					enu.MoveNext();
					c.PhoneNumber = (string)enu.Current;
					enu.MoveNext();
					c.ContactPerson = (string)enu.Current;
					gfax.Destinations.Add(c);
				}
			} 
			else {	
				bsdest = bs.GetSelections(COLUMN_0);
				if ( bsdest.Count > 0 ) {
					IEnumerator enu = bsdest.GetEnumerator();
					while ( enu.MoveNext() ) {
						foreach (Phonebook p in myPhoneBooks)
						if (p.Name == (string)enu.Current)
							contacts = Phonetools.get_contacts(p);

						// add contacts to global desinations
						if (contacts.Count > 0) {
							IEnumerator enuc = contacts.GetEnumerator();
							while ( enuc.MoveNext() )
								gfax.Destinations.Add((GfaxContact)enuc.Current);
						}
					}
				}
			}
				
			phbd.Destroy();
		}

		// loads the phone book into list_store
		private void load_phone_book(Phonebook p)
		{
			ArrayList contacts = null;
						
			// Clear the list_store
			list_store.Clear();
			
			contacts = Phonetools.get_contacts(p);
			if (contacts == null)
				return;
				
			IEnumerator enu = contacts.GetEnumerator();
			while ( enu.MoveNext() ) {
				GfaxContact c = new GfaxContact();
				c = (GfaxContact)enu.Current;
				ls.AddTextToRow(c.Organization, c.PhoneNumber, c.ContactPerson);
			}
		}

		private void on_cancel_button_clicked(object o, EventArgs args)
		{
			phbd.Destroy();
		}

	}
	
}
