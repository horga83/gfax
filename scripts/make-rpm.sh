#!/bin/bash


VERSION=`grep Version: data/gfax.spec | awk '{print $2}'`
rm -rf /usr/src/redhat/SOURCES/gfax-$VERSION.tar.gz
rm -rf /usr/src/redhat/SPECS/gfax.spec
rm -rf /usr/src/redhat/BUILD/gfax-$VERSION
rm -rf /usr/src/redhat/SRPMS/gfax-$VERSION.src.rpm

make clean

cd /home/george/Projects
rm -rf gfax-$VERSION gfax-$VERSION.tar.gz
cp -a gfax gfax-$VERSION
tar cvzf gfax-$VERSION.tar.gz gfax-$VERSION


cp gfax-$VERSION.tar.gz /usr/src/redhat/SOURCES
rpmbuild -ba gfax-$VERSION/data/gfax.spec


