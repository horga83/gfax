#!/bin/bash

#	echo "Usage example: make-deb.sh"

echo "Not used in a long time, would have to be fixed first.."
echo "Check the Ubuntu script..."
exit

ver=`grep Version gfax/data/DEBIAN/control | cut -f2 -d" "`
version="gfax_"${ver}"_i386"
tarversion="gfax-"${ver}".tar.gz"
echo $version

cd gfax
make clean
./configure --prefix=/usr --sysconfdir=/etc
make
sudo make install
cd ..


mkdir $version
mkdir $version/DEBIAN
mkdir $version/etc
mkdir $version/etc/gconf
mkdir $version/etc/gconf/schemas
cp /etc/gconf/schemas/gfax.schemas $version/etc/gconf/schemas/

mkdir $version/usr
mkdir $version/usr/bin
mkdir $version/usr/lib
mkdir $version/usr/lib/gfax
mkdir $version/usr/share
mkdir $version/usr/share/doc
mkdir $version/usr/share/doc/gfax
mkdir $version/usr/share/applications
#mkdir $version/usr/share/libgnomeprint
#mkdir $version/usr/share/libgnomeprint/models
#mkdir $version/usr/share/libgnomeprint/printers

mkdir $version/usr/share/locale
mkdir $version/usr/share/locale/en_CA
mkdir $version/usr/share/locale/en_CA/LC_MESSAGES
mkdir $version/usr/share/locale/de
mkdir $version/usr/share/locale/de/LC_MESSAGES

mkdir $version/usr/share/pixmaps
cp /usr/bin/gfax $version/usr/bin/
cp /usr/bin/gfaxlpr $version/usr/bin/
cp /usr/lib/gfax/gfax.exe $version/usr/lib/gfax/
cp ./gfax/data/fax-g3.profile $version/usr/lib/gfax/
cp ./gfax/data/GFAX.xml $version/usr/lib/gfax/
cp ./gfax/data/GNOME-GFAX-PS.xml $version/usr/lib/gfax/
cp ./gfax/INSTALL $version/usr/share/doc/gfax/
cp ./gfax/NEWS $version/usr/share/doc/gfax/
cp ./gfax/FAQ $version/usr/share/doc/gfax/
cp ./gfax/README $version/usr/share/doc/gfax/
cp ./gfax/AUTHORS $version/usr/share/doc/gfax/
cp /usr/share/applications/gfax.desktop $version/usr/share/applications/
cp /usr/share/pixmaps/gfax.png $version/usr/share/pixmaps/
cp /usr/share/pixmaps/send.png $version/usr/share/pixmaps/


cp /usr/share/locale/en_CA/LC_MESSAGES/gfax.mo $version/usr/share/locale/en_CA/LC_MESSAGES/
cp /usr/share/locale/de/LC_MESSAGES/gfax.mo $version/usr/share/locale/de/LC_MESSAGES/



md5sum /etc/gconf/schemas/gfax.schemas > $version/DEBIAN/md5sums 
md5sum /usr/bin/gfax >> $version/DEBIAN/md5sums 
md5sum /usr/bin/gfaxlpr >> $version/DEBIAN/md5sums
md5sum /usr/lib/gfax/gfax.exe >> $version/DEBIAN/md5sums
md5sum /usr/share/applications/gfax.desktop >> $version/DEBIAN/md5sums
###md5sum /usr/share/libgnomeprint/models/GNOME-GFAX-PS.xml >> $version/DEBIAN/md5sums
###md5sum /usr/share/libgnomeprint/printers/GFAX.xml >> $version/DEBIAN/md5sums
md5sum /usr/share/pixmaps/gfax.png >> $version/DEBIAN/md5sums
md5sum /usr/share/pixmaps/send.png >> $version/DEBIAN/md5sums
md5sum /usr/share/doc/gfax/FAQ >> $version/DEBIAN/md5sums
md5sum /usr/share/doc/gfax/NEWS >> $version/DEBIAN/md5sums
md5sum /usr/share/doc/gfax/INSTALL >> $version/DEBIAN/md5sums
md5sum /usr/share/doc/gfax/README >> $version/DEBIAN/md5sums
md5sum /usr/share/doc/gfax/AUTHORS >> $version/DEBIAN/md5sums
md5sum ./gfax/data/fax-g3.profile >> $version/DEBIAN/md5sums
md5sum ./gfax/data/GFAX.xml >> $version/DEBIAN/md5sums
md5sum ./gfax/data/GNOME-GFAX-PS.xml >> $version/DEBIAN/md5sums

cp ./gfax/data/DEBIAN/* $version/DEBIAN/

dpkg-deb --build $version

cd gfax
make clean
cd ..
tar cvzf $tarversion gfax
