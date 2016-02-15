# created by RPM Builder for Anjuta, v0.1.2
# http://arpmbuilder.sourceforge.net
# Thu Dec 11 20:34:57 2003

%define gfax		0.7.5
#%define anjuta_plugindir	/usr/lib/anjuta
%define _prefix	/usr

Summary:         Gnome facsimile software
Name:            gfax
Version:         0.7.5
Release:         1
Vendor:          George Farris (george@gmsys.com>
Packager:        George Farris (george@gmsys.com>
Group:           Office
License:         GPL
Source0:         %{name}-%{version}.tar.gz
Url:             http://gfax.cowlug.org
BuildRoot:       /var/tmp/gfax
BuildArch:       noarch
Requires:        mono >= 1.0, gtk-sharp >= 1.0, efax >= .9
# Buildrequires:   (none)
# Conflicts:       (none)
# Provides:        (none)
Obsoletes:       gfax

%description 
Gfax is a popup tool for easily sending
facsimilies by printing to a fax printer.

%prep
%setup -q

%build
[ ! -f Makefile ] || make clean
make schema
make

%install
rm -rf $RPM_BUILD_ROOT
mkdir -p $RPM_BUILD_ROOT/etc/gconf/schemas
mkdir -p $RPM_BUILD_ROOT/usr/bin
mkdir -p $RPM_BUILD_ROOT/usr/lib/gfax
mkdir -p $RPM_BUILD_ROOT/usr/share/gfax
mkdir -p $RPM_BUILD_ROOT/usr/share/pixmaps
mkdir -p $RPM_BUILD_ROOT/usr/share/applications
mkdir -p $RPM_BUILD_ROOT/var/spool/gfax

install -m 644 data/gfax.schema $RPM_BUILD_ROOT/etc/gconf/schemas/gfax.schemas
install -m 755 src/gfax.exe $RPM_BUILD_ROOT/usr/lib/gfax/gfax.exe
install -m 755 gfax $RPM_BUILD_ROOT/usr/bin/gfax
install -m 755 gfaxlpr $RPM_BUILD_ROOT/usr/bin/gfaxlpr
install -m 644 pixmaps/gfax.png $RPM_BUILD_ROOT/usr/share/pixmaps/gfax.png
install -m 644 data/gfax.desktop $RPM_BUILD_ROOT/usr/share/applications/gfax.desktop
install -m 755 scripts/printer-setup.sh $RPM_BUILD_ROOT/usr/share/gfax/printer-setup.sh
install -m 644 data/fax-g3.profile $RPM_BUILD_ROOT/usr/share/gfax/fax-g3.profile
install -m 644 data/GFAX.xml $RPM_BUILD_ROOT/usr/share/gfax/GFAX.xml
install -m 644 data/GNOME-GFAX-PS.xml $RPM_BUILD_ROOT/usr/share/gfax/GNOME-GFAX-PS.xml

chmod 777 $RPM_BUILD_ROOT/var/spool/gfax

#[ -n "$RPM_BUILD_ROOT" -a "$RPM_BUILD_ROOT" != "/" ] && rm -rf $RPM_BUILD_ROOT

for doc in AUTHORS COPYING README INSTALL NEWS TODO ChangeLog; do
	rm -f $RPM_BUILD_ROOT%{_prefix}/doc/gfax/$doc;
done;

rm -f $RPM_BUILD_ROOT%{gfax};

%clean
[ -n "$RPM_BUILD_ROOT" -a "$RPM_BUILD_ROOT" != "/" ] && rm -rf $RPM_BUILD_ROOT

%post
export GCONF_CONFIG_SOURCE=`gconftool-2 --get-default-source`
gconftool-2 --makefile-install-rule %{_sysconfdir}/gconf/schemas/gfax.schemas > /dev/null
killall -HUP gconfd-2
# run the printer install
%{_datadir}/gfax/printer-setup.sh --install

%preun
%{_datadir}/gfax/printer-setup.sh --remove

%files 
%defattr(-,root,root)
%config %{_sysconfdir}/gconf/schemas/*.schemas
%{_libdir}/gfax/gfax.exe
%{_bindir}/gfax
%{_bindir}/gfaxlpr
%{_datadir}/applications/*.desktop
%{_datadir}/pixmaps/*
%{_datadir}/gfax/*
%defattr(-, root, root, 0777)
%{_localstatedir}/spool/gfax/

%doc AUTHORS COPYING ChangeLog README INSTALL NEWS TODO
