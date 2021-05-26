




all: darkcodeicons

CODEICONS=$(wildcard wwwroot/images/code/*16x.svg)
DARKCODEICONS=$(patsubst %16x.svg,%16x_dark.svg,$(CODEICONS))

darkcodeicons: $(DARKCODEICONS)

%16x_dark.svg: %16x.svg Makefile
	@sed -e 's/#F6F6F6/#0A0A0A/ig' -e 's/#424242/#BDBDBD/ig' -e 's/#F0EFF1/#0F100E/ig' $< > $@






