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
	using GLib;
	using Gtk;
	using GtkSharp;
	using System.Collections;
	using System.Reflection;
	using Evolution;
	
	//************************************************************************
	//
	// 
	// 
	public class EdsPhoneBooks
	{
	
		private void OnContactsAdded (object o,	Evolution.ContactsAddedArgs args)
		{
			Console.WriteLine ("Contacts added:");
			foreach (Evolution.Contact contact in args.Contacts) {
				Console.WriteLine ("\nId: {0}", contact.Id);
				Console.WriteLine ("Fullname: {0}", contact.FullName);
			}
		}

        private void OnContactsChanged (object o, Evolution.ContactsChangedArgs args)
		{
		}

        private void OnContactsRemoved (object o, Evolution.ContactsRemovedArgs args)
		{
		
		}

		public ArrayList GetPhoneBooks ()
		{
			ArrayList ebooks = new ArrayList();
						
			SourceList slist = new SourceList ("/apps/evolution/addressbook/sources");
			if (slist != null) {
				SList group_list = slist.Groups;
				//Console.WriteLine ("Group count: {0}", group_list.Count);

				foreach (SourceGroup group in group_list) {
					//Only get phone books on this machine.
					if (group.Name == "On This Computer") {
						SList src_list = group.Sources;
					
						foreach (Evolution.Source src in src_list) {
							ebooks.Add(src.Name);
						}
					}
				}
			}
			return ebooks;
		}

		
		public ArrayList GetContacts (string bookName)
		{
			string contact_fax = null;
			ArrayList ebooks = new ArrayList();
			ArrayList records = new ArrayList();
			
			SourceList slist = new SourceList ("/apps/evolution/addressbook/sources");
			if (slist != null) {
				SList group_list = slist.Groups;
				foreach (SourceGroup group in group_list) {
					//Only get phone books on this machine.
					if (group.Name == "On This Computer") {
						SList src_list = group.Sources;
					
						foreach (Evolution.Source src in src_list) {
							if (src.Name == bookName) {
								//Book bk = Book.NewSystemAddressbook ();
								Book bk = new Book(src);
								bk.Open (true);

								BookQuery q = BookQuery.AnyFieldContains ("");
								Contact[] contactlist = bk.GetContacts (q);
								//Console.WriteLine ("Contact count (range) : {0}", contactlist.Length);
								
								if (contactlist != null) {
									
									foreach (Contact comp in contactlist) {
										contact_fax = null;
										
										if (comp.BusinessFax != null && comp.BusinessFax != String.Empty) {
											contact_fax = comp.BusinessFax;
										}
										else if (comp.OtherFax != null && comp.OtherFax != String.Empty) {
											contact_fax = comp.OtherFax;
										}
										else if (comp.HomeFax != null && comp.HomeFax != String.Empty) {
											contact_fax = comp.HomeFax;
										}

										if (contact_fax != null) {
											GfaxContact gc = new GfaxContact();
											//Console.WriteLine ("Id: {0}", comp.Id);
											gc.PhoneNumber = contact_fax;
											gc.ContactPerson = comp.FullName;
											gc.Organization = comp.Org;
											records.Add(gc);
										}
									}
								}
							}
						}
					}
				}
			}
			return records;
		}
	}
}
