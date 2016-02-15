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
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA 02111-1307, USA.

//************************************************************************
// NewPhoneBook class
//
// A new phone book druid, should make it easier for people to create
// new phone books.  The old way was a little confusing.
//

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
	
	public class NewPhoneBook
	{
		[Glade.Widget] Gtk.Dialog NewPhoneBookDialog;
		[Glade.Widget] Gnome.Druid NewPhoneBookDruid;
    	[Glade.Widget] Gnome.DruidPage EvoDruidPageStandard;
    	[Glade.Widget] Gnome.DruidPage druidpagestandard12;
        [Glade.Widget] Gtk.RadioButton GfaxRadioButton;
        [Glade.Widget] Gtk.RadioButton EvolutionRadioButton;
        [Glade.Widget] Gtk.RadioButton DatabaseRadioButton;
	    [Glade.Widget] Gtk.RadioButton LDAPRadioButton;
	    [Glade.Widget] Gtk.Entry NewPhoneBookNameEntry;
		[Glade.Widget] Gtk.TreeView EvolutionTreeView;
	
		// Properties
		ArrayList booknames;
		string phonebooktype;
		
		Gtk.ListStore store;
		G_ListView view;
	
		const int COLUMN_0 = 0;
		const int COLUMN_1 = 1;
    
		public NewPhoneBook ()
	    {
			booknames = new ArrayList();
    		
			Glade.XML xml = new Glade.XML (null, "gfax.glade","NewPhoneBookDialog",null);
            xml.Autoconnect (this);
    
            GfaxRadioButton.Active = true;
              
	        EvolutionRadioButton.Sensitive = true;
            // turn these off until somewhere near supported
			DatabaseRadioButton.Visible = false;
	        LDAPRadioButton.Visible = false;
			
			store = new ListStore(
				typeof (Boolean),
			typeof (string));
	
			view = new G_ListView(EvolutionTreeView, store);
	
			view.AddColumnTitleToggle(Catalog.GetString("Use"), 0, COLUMN_0);
			view.AddColumnTitle(Catalog.GetString("Phone Book"), 0, COLUMN_1);
	
			EvolutionTreeView.HeadersVisible = true;
		
			NewPhoneBookDruid.ShowAll();
	    }
    
		public ArrayList PhoneBookNames
		{
			get { return booknames; }
		}
	
		public string PhoneBookType
		{
			get { return phonebooktype; }
		}
	
    	public void Run()
    	{
    	    NewPhoneBookDialog.Run();
    	}
          
    	private void on_NewPhoneBookDialog_delete_event (object o, DeleteEventArgs args)
		{
			NewPhoneBookDialog.Hide();
			NewPhoneBookDialog.Dispose();
    	    args.RetVal = true;
    	}

    	private void on_NewPhoneBookDruidEdge_finish (object o, Gnome.FinishClickedArgs args)
        {
	                     
	        if (GfaxRadioButton.Active) {
				phonebooktype = "gfax";
    			booknames.Add( NewPhoneBookNameEntry.Text );
			} else if (EvolutionRadioButton.Active) {
	    	    phonebooktype = "evolution";
					
				Gtk.TreeIter iter = new Gtk.TreeIter();
			
				// Get the first row.
				store.GetIterFirst(out iter);
	
				try {
					if ( (bool)store.GetValue(iter, 0) ) {	// if use is true (toggle set)
						booknames.Add( (string)store.GetValue(iter, 1) );
					}
				} catch (Exception e) {
				  Console.WriteLine("[newphonebook.NewPhoneBookDruidPage_finish] Exception: {0}", e);
				}
			
				// get the rest of the rows			
				while (store.IterNext(ref iter)) {
	
					try {
						if ( (bool)store.GetValue(iter, 0) ) {
							booknames.Add( (string)store.GetValue(iter, 1) );
						}
					}
    				catch (Exception e) {
    				  Console.WriteLine("[newphonebook.NewPhoneBookDruidPage_finish] Exception: {0}", e);
    				}
    			}
			}
		    else if (DatabaseRadioButton.Active)
                phonebooktype = "sql";
	    	else if (LDAPRadioButton.Active)
	            phonebooktype = "ldap";
			
			NewPhoneBookDialog.Hide();
			NewPhoneBookDialog.Dispose();
    	}
	
	    private void on_NewPhoneBookDruid_cancel (object o, EventArgs args)
	    {
			NewPhoneBookDialog.Hide();
			NewPhoneBookDialog.Dispose();
		}
		
		private void on_druidpagestandard12_next (object o, Gnome.NextClickedArgs args)
		{	
			// we're on the gfax phone book enter name pages
			// skip to finish on next signal
			NewPhoneBookDruid.Page = EvoDruidPageStandard;
		}
		
		
		private void on_BookDruidPageStandard_next (object o, Gnome.NextClickedArgs args)
		{
			// skip next page if active
			if (EvolutionRadioButton.Active) {
				NewPhoneBookDruid.Page = druidpagestandard12;

				EdsPhoneBooks eds = new EdsPhoneBooks();
				ArrayList ebooks = new ArrayList();
				ebooks = eds.GetPhoneBooks();
		
				Gtk.TreeIter iter = new Gtk.TreeIter();
			
				IEnumerator enu = ebooks.GetEnumerator();
	  			while ( enu.MoveNext() ) {
					iter = store.AppendValues(false, enu.Current);
					EvolutionTreeView.Model = store;
				}
			}
		}
	}
}
