#!/bin/bash

if [ ! -d /var/spool/gfax ]; then
   mkdir -m 777 /var/spool/gfax
   chown root:root /var/spool/gfax
fi
cp -f /usr/lib/gfax/cups-gfax /usr/lib/cups/backend/
chmod 755 /usr/lib/cups/backend/cups-gfax
ln -sf /usr/lib/cups/backend/cups-gfax /usr/lib/cups/backend-available/cups-gfax
lpadmin -p Gfax_Facsimile_Printer -v cups-gfax:/ -m lsb/usr/cups-included/postscript.ppd -E
gconf-schemas --register /etc/gconf/schemas/gfax.schemas
