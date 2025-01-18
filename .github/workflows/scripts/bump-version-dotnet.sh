sudo apt-get update
sudo apt-get install xmlstarlet -y

CSPROJ_FILE=$1
CURRENT_MONTH=$(date +'%y%m')
VERSION_XML_PATH="Project/PropertyGroup/Version"

LAST_VERSION=$(xmlstarlet select --text --template --value-of $VERSION_XML_PATH "$CSPROJ_FILE")
LAST_VERSION_MONTH=$(echo "$LAST_VERSION" | cut -d "." -f 1)
LAST_VERSION_NUMBER=$(echo "$LAST_VERSION" | cut -d "." -f 2)

if [[ $CURRENT_MONTH -eq $LAST_VERSION_MONTH ]]; then NEW_VERSION=$LAST_VERSION_MONTH.$((LAST_VERSION_NUMBER+1)); fi
if [[ $CURRENT_MONTH -gt $LAST_VERSION_MONTH ]]; then NEW_VERSION=$CURRENT_MONTH.0; fi
if [[ $CURRENT_MONTH -lt $LAST_VERSION_MONTH ]]; then exit 1; fi

xmlstarlet edit --inplace --omit-decl --update $VERSION_XML_PATH -v "$NEW_VERSION" "$CSPROJ_FILE"
git status
git diff

COMMIT_MESSAGE="Version bump from $LAST_VERSION to $NEW_VERSION"

git config user.name "${GITHUB_ACTOR}"
git config user.email "${GITHUB_ACTOR}@users.noreply.github.com"
git add .
git commit -m "$COMMIT_MESSAGE [skip ci]"
git tag -a "$NEW_VERSION" -m "Version $NEW_VERSION"
git push --force origin master --tags
