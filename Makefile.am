SUBDIRS = data src po

desktop_in_files=gfax.desktop.in
desktop_files=$(desktop_in_files:.desktop.in=.desktop)
@INTLTOOL_DESKTOP_RULE@

Applicationsdir = $(datadir)/applications
Applications_DATA = $(desktop_files)

schemas_in_files = gfax.schemas.in
schemasdir	 = $(GCONF_SCHEMA_FILE_DIR)
schemas_DATA  = $(schemas_in_files:.schemas.in=.schemas)


@INTLTOOL_SCHEMAS_RULE@


if GCONF_SCHEMAS_INSTALL
install-data-local:
	if test -z "$(DESTDIR)" ; then \
	  for p in $(schemas_DATA) ; do \
	    GCONF_CONFIG_SOURCE=$(GCONF_SCHEMA_CONFIG_SOURCE) $(GCONFTOOL) --makefile-install-rule $(srcdir)/$$p ; \
	  done \
	fi
else
install-data-local:
endif

bin_SCRIPTS = gfax
bindir=$(prefix)/bin

gfax: gfax.in config.log
	sed -e "s|\@prefix\@|$(prefix)|" 	\
		-e "s|\@eprefix\@|$(eprefix)|" < gfax.in > gfax

#        lpadmin -p Gfax_Facsimile_Printer -v cups-gfax:/ -E
#        lpadmin -x Gfax_Facsimile_Printer

INTLTOOL =                      \
        intltool-extract.in     \
        intltool-merge.in       \
        intltool-update.in

EXTRA_DIST = \
        $(desktop_in_files)     \
        $(desktop_files)        \
        $(schemas_in_files)     \
        $(schemas_DATA)         \
	$(backenddir_EXEC)		\
	$(backenddir_available_EXEC)	\
	gfax.in                 \
	$(INTLTOOL)

CLEANFILES = \
	     gfax.exe	\
		 gfax

DISTCLEANFILES = \
        intltool-extract        \
        intltool-merge          \
        intltool-update         \
        $(desktop_files)        \
        $(schemas_DATA)
