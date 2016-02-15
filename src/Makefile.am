# 0169 - Don't warn about unused functions, they are called from libglade
# 0219 - Constructs like Gtk.TreeIter iter = new Gtk.TreeIter();
CSC = gmcs -nowarn:0169,0219 -debug

TARGET = gfax.exe
TARGET1 = cups-gfax
WRAPPER = gfax
SCHEMA = $(top_srcdir)/gfax.schemas.in

#CSFLAGS = -define:DEBUGEFAX
#CSFLAGS = -define:DEBUGHYLAFAX
#CSFLAGS =

ASSEMBLIES = -r:Mono.Posix

RESOURCES = \
	-resource:$(top_srcdir)/glade/gfax.glade,gfax.glade \
	-resource:$(top_srcdir)/glade/send-druid.glade,send-druid.glade \
	-resource:$(top_srcdir)/pixmaps/gfax.png,gfax.png \
	-resource:$(top_srcdir)/pixmaps/send.png,send.png


CSFILES = efax.cs 		\
	fax.cs 	\
	gui.cs 	\
	gfaxprefs.cs \
	gfaxsend.cs \
	gfaxphonebook.cs \
	newphonebook.cs \
	guitools.cs 	\
	hylafax.cs	\
	main.cs 		\
	phonebook.cs 		\
	evolution.cs 	\
	sendphonebook.cs 
	

GENERATED_CSFILES = Defines.cs Settings.cs

Defines.cs: Defines.cs.in ../config.log
	sed -e "s|\@version\@|$(VERSION)|" \
	    -e "s|\@datadir\@|$(datadir)|" \
		-e "s|\@eprefix\@|$(eprefix)|" \
	    < Defines.cs.in > Defines.cs

Settings.cs: Settings.cs
	env GCONF_CONFIG_SOURCE="" gconftool-2 --makefile-install-rule $(SCHEMA); \
	gconfsharp2-schemagen gfax $(SCHEMA) > $(srcdir)/Settings.cs

gfax.exe: $(CSFILES) $(GENERATED_CSFILES)
	$(CSC) $(CSFLAGS) $(GFAX_LIBS) $(ASSEMBLIES) $(RESOURCES) $(CSFILES) $(GENERATED_CSFILES) -o $@ 

cups-gfax: cups-gfax ../config.log
	cp cups-gfax.in cups-gfax

all: gfax.exe cups-gfax

install-data-local: $(TARGET) $(TARGET1)
	$(mkinstalldirs) $(DESTDIR)$(pkglibdir)
	$(INSTALL_PROGRAM) $(TARGET) $(DESTDIR)$(pkglibdir)/$(TARGET)
	$(INSTALL_PROGRAM) $(TARGET1) $(DESTDIR)$(pkglibdir)/$(TARGET1)

uninstall-local:
	rm -f $(DESTDIR)$(pkglibdir)/$(TARGET)
	rm -f $(DESTDIR)$(pkglibdir)/$(TARGET1)

EXTRA_DIST =            		\
	$(srcdir)/Defines.cs.in		\
	$(CSFILES)      			


CLEANFILES =	\
	gfax.exe	\
	Defines.cs	\
	Settings.cs
