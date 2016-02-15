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
	// GfaxPrefs class
	//
	// This is the preferences window.
	public class GfaxPrefs
	{
		Glade.XML gxml;
		[Glade.Widget] Gtk.Dialog PrefsDialog;
		[Glade.Widget] Gtk.Notebook PrefsNotebook;
		//System Tab
		[Glade.Widget] Gtk.RadioButton HylafaxRadioButton;
		[Glade.Widget] Gtk.RadioButton EfaxRadioButton;
		[Glade.Widget] Gtk.Entry FaxNumberEntry;
		[Glade.Widget] Gtk.Entry DialPrefixEntry;
		[Glade.Widget] Gtk.Entry ModemEntry;
		[Glade.Widget] Gtk.Entry FaxViewerEntry;
		
		//Hylafax Tab
		[Glade.Widget] Gtk.Entry HylafaxHostnameEntry;
		[Glade.Widget] Gtk.Entry HylafaxPortEntry;
		[Glade.Widget] Gtk.Entry HylafaxUsernameEntry;
		
		//Efax Tab
		[Glade.Widget] Gtk.Entry EfaxModemDeviceEntry;
		[Glade.Widget] Gtk.ComboBox EfaxPapersizeComboBox;
		[Glade.Widget] Gtk.ComboBox EfaxModemTypeComboBox;
		[Glade.Widget] Gtk.ComboBox EfaxModemSpeakerVolumeComboBox;
		
		//User Tab
		[Glade.Widget] Gtk.CheckButton EmailNotifyCheckButton;
		[Glade.Widget] Gtk.Entry	EmailAddressEntry;
		[Glade.Widget] Gtk.CheckButton SendNowCheckButton;
		[Glade.Widget] Gtk.CheckButton FaxLogCheckButton;
		[Glade.Widget] Gtk.CheckButton CoverPageCheckButton;
		[Glade.Widget] Gtk.CheckButton HiResCheckButton;
	
		// we set this otherwise setting inital values will wipe
		// out all the other settings.
		bool eventsEnabled = false; 
	
		bool taChanged = false;
		
		const int HYLAFAX_PAGE = 1;
		const int EFAX_PAGE = 2;
		
		//string[] papersize = {"letter", "legal", "a4"};
		//string[] modemType = {@"-j\Q4",@"-j\Q1",@"-j*F1",@"-j&H2&I0&R1&D3I4",@"-or"};
		
		/*
		# FCINIT='-j\Q4'                # AT&T (Dataport, Paradyne)
		# FCINIT='-j\Q1'                # Motorola (Power Modem, 3400 Pro,...)
		# FCINIT='-j*F1'                # QuickComm (Spirit II)
		# FCINIT='-j&H2&I0&R1&D3I4'     # USR (Courier, Sportster)
		# FCINIT='-or'                  # Multi-Tech (for bit reversal)
		*/
		
		
		public GfaxPrefs ()
		{
			gxml = new Glade.XML (null, "gfax.glade","PrefsDialog",null);
			//GConf.PropertEditors
			EditorShell shell = new EditorShell (gxml);
			gxml.Autoconnect(this);
			
			
			// System Tab
			if (Settings.TransmitAgent == "hylafax") {
				HylafaxRadioButton.Active = true;
				CoverPageCheckButton.Visible = false;
				PrefsNotebook.GetNthPage(EFAX_PAGE).Hide();
			}
			else if (Settings.TransmitAgent == "efax") {
				EfaxRadioButton.Active = true;
				EmailNotifyCheckButton.Sensitive = false;
				EmailAddressEntry.Sensitive = false;
				Settings.SendNow = SendNowCheckButton.Active;
				FaxLogCheckButton.Visible = false;
				CoverPageCheckButton.Visible = false;
				Settings.HiResolution = HiResCheckButton.Active;
	
				PrefsNotebook.GetNthPage(HYLAFAX_PAGE).Hide();
			}
			
			// Set these regardless so they are set if we need them
			switch (Settings.EfaxPapersize) {
				case "letter":
					EfaxPapersizeComboBox.Active = 0;
					break;
				case "legal":
					EfaxPapersizeComboBox.Active = 1;
					break;
				case "a4":
					EfaxPapersizeComboBox.Active = 2;
					break;
				default:
					EfaxPapersizeComboBox.Active = 0;
					break;
			}
			//"-j\\Q4","-j\\Q1","-j*F1","-j&H2&I0&R1&D3I4","-or"
			switch (Settings.EfaxModemFcinit) {
					case @"-j\Q4":
						EfaxModemTypeComboBox.Active = 0;
						break;
					case @"-j\Q1":
						EfaxModemTypeComboBox.Active = 1;
						break;
					case @"-j*F1":
						EfaxModemTypeComboBox.Active = 2;
						break;
					case @"-j&H2&I0&R1&D3I4":
						EfaxModemTypeComboBox.Active = 3;
						break;
					case @"-or":
						EfaxModemTypeComboBox.Active = 4;
						break;
					default:
						EfaxModemTypeComboBox.Active = 5;
						break;
			}
									
			EfaxModemSpeakerVolumeComboBox.Active = Settings.EfaxModemSpeakerVolume;
			
				
				
			// changes that happen automagically
			shell.Add(SettingKeys.FaxNumber, "FaxNumberEntry");
			shell.Add(SettingKeys.PhonePrefix, "DialPrefixEntry");
			shell.Add(SettingKeys.FaxViewer, "FaxViewerEntry");
			
			// Hylafax Tab
			shell.Add(SettingKeys.Hostname, "HylafaxHostnameEntry");
			shell.Add(SettingKeys.Port, "HylafaxPortEntry");
			shell.Add(SettingKeys.Username, "HylafaxUsernameEntry");
			
			// Efax Tab
			shell.Add(SettingKeys.EfaxModemDevice, "EfaxModemDeviceEntry");
			
			
			// User tab
			shell.Add(SettingKeys.EmailNotify, "EmailNotifyCheckButton");
			shell.Add(SettingKeys.EmailAddress, "EmailAddressEntry");
			shell.Add(SettingKeys.SendNow, "SendNowCheckButton");
			shell.Add(SettingKeys.LogEnabled, "FaxLogCheckButton");
			shell.Add(SettingKeys.CoverPage, "CoverPageCheckButton");
			shell.Add(SettingKeys.HiResolution, "HiResCheckButton");
	
			eventsEnabled = true;
	
		}
			
		
		private void on_PrefsDialog_delete_event (object o, DeleteEventArgs args) 
		{
			MessageDialog md;
			if (taChanged) {
				md = new MessageDialog (
					null,
					DialogFlags.DestroyWithParent, 
					MessageType.Warning, 
					ButtonsType.Ok,
					Catalog.GetString("You need to exit Gfax and restart it when you change transport agents")
				);
				md.Run ();
				md.Destroy();
			}					
			PrefsDialog.Destroy();
			args.RetVal = true;
		}
	
		private void on_CloseButton_clicked (object o, EventArgs args) 
		{	
			MessageDialog md;
			if (taChanged) {
				md = new MessageDialog (
					null,
					DialogFlags.DestroyWithParent, 
					MessageType.Warning, 
					ButtonsType.Ok,
					Catalog.GetString("You need to exit Gfax and restart it when you change transport agents")
				);
				md.Run ();
				md.Destroy();
			}					
			PrefsDialog.Destroy();
		}
		
		private void on_HelpButton_clicked (object o, EventArgs args) 
		{
			
		}
		
		private void system_tab_changed (object o, EventArgs args) 
		{
			if (eventsEnabled) {
				//eventsEnabled = false;
				if (HylafaxRadioButton.Active) {
					Settings.TransmitAgent = "hylafax";
					Settings.RefreshQueueInterval = 30;
					Settings.RefreshQueueEnabled = false;
					PrefsNotebook.GetNthPage(HYLAFAX_PAGE).Show();
					PrefsNotebook.GetNthPage(EFAX_PAGE).Hide();
				}					
				else if (EfaxRadioButton.Active) {
					Settings.TransmitAgent = "efax";
					Settings.RefreshQueueInterval = 15;
					Settings.RefreshQueueEnabled = true;
					PrefsNotebook.GetNthPage(EFAX_PAGE).Show();
					PrefsNotebook.GetNthPage(HYLAFAX_PAGE).Hide();

				}
	
				Settings.FaxNumber = FaxNumberEntry.Text;
				Settings.PhonePrefix = DialPrefixEntry.Text;
				Settings.FaxViewer = FaxViewerEntry.Text;
			}
		}
		
		private void user_tab_changed (object o, EventArgs args) 
		{
			if (eventsEnabled) {
				Settings.EmailNotify = EmailNotifyCheckButton.Active;
				Settings.EmailAddress = EmailAddressEntry.Text;
				Settings.SendNow = SendNowCheckButton.Active;
				Settings.LogEnabled = FaxLogCheckButton.Active;
				Settings.CoverPage = CoverPageCheckButton.Active;
				Settings.HiResolution = HiResCheckButton.Active;
			}
		}
		
		private void radio_button_toggled (object o, EventArgs args) 
		{	
			if (eventsEnabled)
				taChanged = true;
		}
	
	
		private void efax_setup_changed (object o, EventArgs args) 
		{	
		
			if (eventsEnabled) {
				// Option 5 is custom define so we don't change anything
				if (EfaxModemTypeComboBox.Active != 5) {
					switch (EfaxModemTypeComboBox.Active) {
						case 0:
							Settings.EfaxModemFcinit = @"-j\Q4";
							break;
						case 1:
							Settings.EfaxModemFcinit = @"-j\Q1";
							break;
						case 2:
							Settings.EfaxModemFcinit = @"-j*F1";
							break;
						case 3:
							Settings.EfaxModemFcinit = @"-j&H2&I0&R1&D3I4";
							break;
						case 4:
							Settings.EfaxModemFcinit = @"-or";
							break;
						default:
							Settings.EfaxModemFcinit = @"-j&H2&I0&R1&D3I4";
							break;
					}
				}
					
				Settings.EfaxPapersize = EfaxPapersizeComboBox.ActiveText;
				Settings.EfaxModemSpeakerVolume = EfaxModemSpeakerVolumeComboBox.Active;
			}
		}
	}
}
