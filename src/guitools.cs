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
	using System;
    using System.IO;
	using System.Collections;
    using System.Reflection;
    using GLib;
	using Gtk;
	using Gdk;
	using Gnome;
	using Glade;
	using GtkSharp;

	// G_ProgressBar
	public class G_ProgressBar
	{
		[Glade.Widget] Gtk.Dialog HylafaxProgressDialog;
		[Glade.Widget] ProgressBar HylafaxProgressbar;
		[Glade.Widget] Gtk.Notebook HylafaxProgressNotebook;
		[Glade.Widget] Gtk.Button HylafaxProgressCancelButton;
		[Glade.Widget] Gtk.Button HylafaxProgressCloseButton;
		
		bool close = true;
		bool cancel = false;	// Cancel all jobs
		
		public G_ProgressBar ()
		{
			Glade.XML xml = new Glade.XML (null, "gfax.glade","HylafaxProgressDialog",null);
			xml.Autoconnect (this);
			
			HylafaxProgressNotebook.CurrentPage = 0;
			HylafaxProgressCloseButton.Hide();
			HylafaxProgressDialog.Show();
		}
		
		public bool Cancel
		{
			get { return cancel; }
		}
		
		private void on_HylafaxProgressCancelButton_clicked(object o, EventArgs args)
		{
			cancel = true;
		}
		
		private void on_HylafaxProgressCloseButton_clicked(object o, EventArgs args)
		{
			close = false;
			HylafaxProgressDialog.Destroy();
		}
		
		public void Close()
		{
			//close = false;
			//HylafaxProgressDialog.Hide();
			//HylafaxProgressDialog.Dispose();
			HylafaxProgressDialog.Destroy();
		}
		
		//public void Run ()
		//{
		//	HylafaxProgressDialog.Run();
		//}
		
		public double Fraction
		{
			set { HylafaxProgressbar.Fraction = value; }
		}
		
		public void Finished()
		{
			HylafaxProgressNotebook.CurrentPage = 1;
			HylafaxProgressCancelButton.Hide();
			HylafaxProgressCloseButton.Show();
			// TODO Bad, bad, slap your wrist boy, do timer function 
			// or fix this somehow.  Can I say 100% processor useage.
			//while (close) {
			//	while (Gtk.Application.EventsPending ())
            //    	Gtk.Application.RunIteration ();
			//}
		}
	}

	// G_Message class
	//
	// A simple message dialog
	public class G_Message
	{
		public G_Message (string s)
		{	
			MessageDialog d = new MessageDialog (
				null, DialogFlags.DestroyWithParent, MessageType.Error, ButtonsType.Ok, s);
			d.Run ();
        	d.Destroy ();
		}
		
		public G_Message (string s, bool toplevel)
		{	
			Application.Init();
			MessageDialog d = new MessageDialog (
				null, DialogFlags.DestroyWithParent, MessageType.Error, ButtonsType.Ok, s);
			d.Run ();
        	d.Destroy ();
			Application.Quit();
		}
	}
	
	public class G_Password
	{
		[Glade.Widget] Gtk.Dialog PasswordDialog;
		[Glade.Widget] Gtk.Entry PasswordEntry;
		
		string pass;			// the password entered
		bool cancel = false;	// Cancel the connection
		bool remember = false;	// Remember the password
		
		public G_Password ()
		{
			Glade.XML xml = new Glade.XML (null, "gfax.glade","PasswordDialog",null);
			xml.Autoconnect (this);
			
			PasswordDialog.Show();
		}
		
		public bool Cancel
		{
			get { return cancel; }
		}
		
		public bool RememberPassword
		{
			get { return remember; }
		}
		
		public string Password
		{
			get { return pass; }
		}
		
		public void Run ()
		{
			PasswordDialog.Run();
		}
		
		private void on_PasswordCheckbutton_toggled(object o, EventArgs args)
		{
			remember = true;
		}
		
		private void on_cancelbutton_clicked(object o, EventArgs args)
		{
			cancel = true;
			PasswordDialog.Hide();
		}
		
		private void on_PasswordEntry_activate(object o, EventArgs args)
		{
			pass = PasswordEntry.Text;	
			PasswordDialog.Hide();
		}
		
		private void on_okbutton_clicked(object o, EventArgs args)
		{
			pass = PasswordEntry.Text;	
			PasswordDialog.Hide();
		}
		
		public void Close()
		{
			PasswordDialog.Destroy();
		}
	}


	// ***********************************************************************
	//
	// G_GetFilename class
	//
	//	Implements a GTK+ fileselector with signal handler for Ok and Cancel
	//	Arguments:
	//		string title	: Title open file selection dialog
	//		string dir		: Starting directory

	public class G_GetFilename
	{
		string fname;
		public Gtk.FileSelection fs;
		
		public G_GetFilename (string title, string dir)
		{
			fs = new FileSelection(title);
			fs.Filename = dir;
			fs.Modal = true;
			fs.HideFileopButtons ();
			fs.OkButton.Clicked += new EventHandler (ok);
			fs.CancelButton.Clicked += new EventHandler (cancel);
			
/*			
           check_button = new CheckButton ("Show Fileops");
           check_button.Toggled += new EventHandler (show_fileops);
			fs.ActionArea.PackStart (check_button, false, false, 0);
    
           toggle_button = new ToggleButton ("Select Multiple");
           toggle_button.Clicked += new EventHandler (select_multiple);
           fs.ActionArea.PackStart (toggle_button, false, false, 0);
*/	
			fs.ShowAll();
			fs.Run ();
		}
		
		public void Destroy ()
		{
			fs.Destroy();
		}
		
		private void cancel (object o, EventArgs args)
		{
			//Console.WriteLine("[Cancel] file selection cancel");
			fs.Hide ();
		}
		
		private void ok (object o, EventArgs args)
		{
			//Console.WriteLine(fs.Filename);
			fname = fs.Filename;
			fs.Hide ();
		}
	
		public string Fname
		{
			get {return fname;}
			set {fname = value;}
		}			
	
		
	}

	public class CellRendererDateTime: CellRendererText
	{
		public string Format;
	}

	public class G_ListView
	{	
		Gtk.ListStore store;
		Gtk.TreeView tree;
		Gtk.TreeViewColumn column;
		Gtk.CellRendererText textrenderer;
		CellRendererDateTime datetimerenderer;
		Gtk.CellRendererPixbuf iconrenderer;
		Gtk.TreeIter iter;
		Value valnot = new Value(false);
		Value valone = new Value(true);

		const int COLUMN_0 = 0;
		const int COLUMN_1 = 1;
		const int COLUMN_2 = 2;
		const int ALL_COLUMNS = -1;	
		
		public G_ListView (Gtk.TreeView tr, Gtk.ListStore st)
		{
			store = st;
			tree = tr;
			iter = new Gtk.TreeIter();
		}
		
		public void AddColumnIcon (string iconname, int col)
		{
			column = new TreeViewColumn ();
			iconrenderer = new Gtk.CellRendererPixbuf ();
			column.Expand = false;
			column.Resizable = false;
			column.Clickable = false;
			column.Reorderable = false;
			column.Alignment = 0.5f;
			column.Widget = new Gtk.Image(
				Gtk.IconTheme.Default.LoadIcon (iconname, 16, 0)
			);
			column.Widget.Show();
			column.PackStart (iconrenderer, true);
			column.AddAttribute (iconrenderer, "pixbuf", col);
			tree.AppendColumn(column);
		}
		
		protected int DateTimeTreeIterCompareFunc (TreeModel model, TreeIter a, TreeIter b)
		{
			int col = -1;
			SortType sort;
			
			bool is_sorted = ((Gtk.ListStore)model).GetSortColumnId (out col, out sort);
			if(!is_sorted)
				return 0;
			
			DateTime dateA = (DateTime)model.GetValue (a, col);
			DateTime dateB = (DateTime)model.GetValue (b, col);
			
			return dateA.CompareTo(dateB);
		}
		
		protected void DateTimeColumnDataHandler (TreeViewColumn column,
			 CellRenderer cell, TreeModel model, TreeIter iter)
		{
			DateTime date = (DateTime)model.GetValue (iter, column.SortColumnId);
			CellRendererDateTime renderer = (CellRendererDateTime)cell;
			renderer.Text = date.ToString(renderer.Format);
		}
		
		public void AddColumnDateTime (string title, string format, int sortid, int col)
		{
			store.SetSortFunc(col, new TreeIterCompareFunc(DateTimeTreeIterCompareFunc));
			
			column = new TreeViewColumn ();
			datetimerenderer = new CellRendererDateTime ();
			datetimerenderer.Format = format == null ? "G" : format;
			column.Title = title;
			column.SortColumnId = sortid;
			column.Sizing = TreeViewColumnSizing.Autosize;
			column.Reorderable = true;
			column.Resizable = true;
			column.Expand = false;
			column.Alignment = 0.0f;
			column.PackStart (datetimerenderer, true);
			column.SetCellDataFunc (datetimerenderer, new TreeCellDataFunc(DateTimeColumnDataHandler));
			tree.AppendColumn(column);
		}
		
		public void AddColumnTitle (string title, int sortid, int col)
		{
			column = new TreeViewColumn ();
			textrenderer = new CellRendererText ();
			//text.Editable = true;object
			column.Title = title;
			column.SortColumnId = sortid;
			column.Sizing = TreeViewColumnSizing.Autosize;
			column.Reorderable = true;
			column.Resizable = true;
			column.Expand = false;
			column.Alignment = 0.0f;
			column.PackStart (textrenderer, true);
			column.AddAttribute (textrenderer, "text", col);
			tree.AppendColumn(column);
		}

// TODO move this to GfaxSend in gui.cs
		public void AddColumnTitleToggle (string title, int sortid, int col)
		{
			column = new TreeViewColumn ();
			CellRendererToggle toggle = new CellRendererToggle ();
			toggle.Activatable = true;
			toggle.Toggled += new ToggledHandler (toggle_it);
			column.Title = title;
			column.PackStart (toggle, false);
			column.AddAttribute (toggle, "active", col);
			tree.AppendColumn(column);
		}

		// we need to manually set the toggle when selected otherwise
		// it won't show in the gui.
		public void toggle_it (object o, Gtk.ToggledArgs args)
		{
			store.GetIterFromString(out iter, args.Path);
			bool tog = (bool)store.GetValue(iter, 0);
			store.SetValue(iter, 0, !tog);
		}
// end of move

		// Each arg is a new column
		public void AddTextToRow (params object[] args)
		{
			iter = store.AppendValues(args);
			tree.Model = store;
		}

		// returns a list of arrays of columns size
		public ArrayList GetAllRows()
		{
			ArrayList rows = new ArrayList();
						
			// Really get the whole row.
			string[] col = new string[store.NColumns];
			store.GetIterFirst(out iter);
			
			for ( int i = 0; i < store.NColumns; i++) {
				try {
					col[i] = (string)store.GetValue(iter, i);
					//Console.WriteLine("Column {0}",col[i]);
				}
				catch (Exception e) { 
				  Console.WriteLine("[newphonebook.GetAllRows] Exception: {0}", e);
				}
			}
			rows.Add(col);
			
			#if DEBUG
			Console.WriteLine("got columns");
			#endif
			
			while (store.IterNext(ref iter)) {
				string[] coln = new string[store.NColumns];
				for ( int i = 0; i < store.NColumns; i++) {
					coln[i] = (string)store.GetValue(iter, i);
				}
				rows.Add(coln);
			}
			return rows;
		}
		
		// get multiple selections, return a list of objects
		public ArrayList GetSelections(int column)
		{
			ArrayList selects = new ArrayList();
			
			// Really get the whole row.
			if (column == ALL_COLUMNS) {
				store.GetIterFirst(out iter);
				if ( tree.Selection.IterIsSelected(iter)) {
					for ( int i = 0; i < store.NColumns; i++) {
						selects.Add(store.GetValue(iter, i));
					}
				}
				
				while (store.IterNext(ref iter)) {
					if ( tree.Selection.IterIsSelected(iter)) {
						for ( int i = 0; i < store.NColumns; i++) {
							selects.Add(store.GetValue(iter, i));
						}
					}
				}
			}
			else
			{
				store.GetIterFirst(out iter);
				if ( tree.Selection.IterIsSelected(iter)) {
					selects.Add(store.GetValue(iter, column));
				}
			
				while (store.IterNext(ref iter)) {
					if ( tree.Selection.IterIsSelected(iter)) {
						selects.Add(store.GetValue(iter, column));
					}
				}
			}
			return selects;
		}
		
		// get single selection
		public object GetSingleSelection ()
		{
			object sel = null;
			Gtk.TreeIter iter = new Gtk.TreeIter();
                        			
			store.GetIterFirst(out iter);
			if ( tree.Selection.IterIsSelected(iter)) {
				sel = store.GetValue(iter, 0);
			}
			
			while (store.IterNext(ref iter)) {
				if ( tree.Selection.IterIsSelected(iter)) {
					sel = store.GetValue(iter, 0);
				}
			}
			return sel;
		}
		
		public void RemoveSelectedRow ()
		{
			Gtk.TreeIter iter = new Gtk.TreeIter();
                        			
			store.GetIterFirst(out iter);
			if ( tree.Selection.IterIsSelected(iter)) {
				store.Remove(ref iter);
				return;
			}
			
			while (store.IterNext(ref iter)) {
				if ( tree.Selection.IterIsSelected(iter)) {
					store.Remove(ref iter);
					break;
				}
			}
			return;
		}
		
		//TODO get this to work
		public void RemoveSelectedRows ()
		{
			Gtk.TreeIter iter = new Gtk.TreeIter();
                        			
			store.GetIterFirst(out iter);
			if ( tree.Selection.IterIsSelected(iter)) {
				store.Remove(ref iter);
			}
			
			while (store.IterNext(ref iter)) {
				if ( tree.Selection.IterIsSelected(iter)) {
					store.Remove(ref iter);
				}
			}
			return;
		}


		public void UpdateColumnText (string oldString, string newString, int column)
		{
			Gtk.TreeIter iter = new Gtk.TreeIter();
                        			
			store.GetIterFirst(out iter);
			// Check for a null Stamp (no initial entry in the list)
			if (iter.Stamp == 0)
				return;
			
			if ( oldString == (string)store.GetValue(iter, column)) {
				GLib.Value val = new GLib.Value(newString);
  				store.SetValue (iter, column, val);
			}

			while (store.IterNext(ref iter)) {
				if ( oldString == (string)store.GetValue(iter, column)) {
					GLib.Value val1 = new GLib.Value(newString);
  					store.SetValue (iter, column, val1);
				}
			}
			
		}
	}
}
