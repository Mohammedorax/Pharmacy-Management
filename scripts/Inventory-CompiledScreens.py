from pathlib import Path
import xml.etree.ElementTree as ET
import re, csv, json, hashlib, argparse, subprocess, base64
from collections import Counter
from datetime import datetime, timezone
parser=argparse.ArgumentParser(description='Inventory compiled Forms and native dialog call sites; no runtime acceptance.')
parser.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[1])
parser.add_argument('--output',type=Path)
parser.add_argument('--assembly',type=Path,help='Optional current application assembly on C:; read type metadata without constructing windows.')
args=parser.parse_args()
root=args.root.resolve()
generated=datetime.now(timezone.utc)
requested_output=args.output or Path('C:/PharmacyQA/work')/('compiled-screen-inventory-'+generated.strftime('%Y%m%d-%H%M%S-%f'))
if not requested_output.is_absolute(): raise SystemExit('Inventory output must be an absolute C: path.')
out=requested_output.resolve()
if out.drive.upper()!='C:': raise SystemExit('Inventory output must be on C:.')
if out.exists(): raise SystemExit('Refusing to overwrite existing inventory: '+str(out))

def read_form_metadata(path):
    path=path.resolve()
    if path.drive.upper()!='C:' or not path.is_file() or path.suffix.lower() not in {'.exe','.dll'}:
        raise SystemExit('Assembly must be an existing managed .exe or .dll on C:.')
    original_hash=hashlib.sha256(path.read_bytes()).hexdigest().upper()
    literal=str(path).replace("'","''")
    script=r'''
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$directory=[IO.Path]::GetDirectoryName('__ASSEMBLY__')
$resolver=[ResolveEventHandler]{param($sender,$event)
    $name=[Reflection.AssemblyName]::new($event.Name)
    $dependency=Join-Path $directory ($name.Name+'.dll')
    if(Test-Path -LiteralPath $dependency -PathType Leaf){return [Reflection.Assembly]::ReflectionOnlyLoadFrom($dependency)}
    return [Reflection.Assembly]::ReflectionOnlyLoad($event.Name)
}
[AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($resolver)
$assembly=[Reflection.Assembly]::ReflectionOnlyLoadFrom('__ASSEMBLY__')
$entries=@(foreach($type in $assembly.GetTypes()) {
    $base=$type.BaseType; $isForm=$false
    while($null -ne $base) {
        if($base.FullName -eq 'System.Windows.Forms.Form' -and $base.Assembly.GetName().Name -eq 'System.Windows.Forms'){$isForm=$true;break}
        $base=$base.BaseType
    }
    if(-not $isForm) {continue}
    $owner=$type
    while($null -ne $owner.DeclaringType) {$owner=$owner.DeclaringType}
    $kind=if($owner.Name -match '(SelfTests|Tests|UiAutomationPreview)$') {'TestFixture'} elseif($type.IsAbstract) {'AbstractSurface'} else {'ApplicationWindow'}
    $constructors=@($type.GetConstructors([Reflection.BindingFlags]'Public,NonPublic,Instance') | ForEach-Object {
        '('+(($_.GetParameters() | ForEach-Object {$_.ParameterType.FullName}) -join ', ')+')'
    })
    [pscustomobject]@{Class=$type.Name;QualifiedClass=$type.FullName;Base=$type.BaseType.Name;BaseFullName=$type.BaseType.FullName;
        SourceOwner=$owner.Name;SurfaceKind=$kind;IsNested=$type.IsNested;Constructors=$constructors}
})
ConvertTo-Json -InputObject $entries -Depth 5 -Compress
'''.replace('__ASSEMBLY__',literal)
    encoded=base64.b64encode(script.encode('utf-16-le')).decode('ascii')
    result=subprocess.run(['powershell.exe','-NoProfile','-NonInteractive','-EncodedCommand',encoded],
        capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=45)
    if result.returncode: raise SystemExit('Assembly metadata inspection failed: '+result.stderr.strip())
    entries=json.loads(result.stdout.lstrip('\ufeff'))
    if hashlib.sha256(path.read_bytes()).hexdigest().upper()!=original_hash:
        raise SystemExit('Assembly changed during inventory; rerun against a stable build.')
    if not isinstance(entries,list) or not entries: raise SystemExit('No compiled Form types found in the supplied assembly.')
    if len({entry['QualifiedClass'] for entry in entries})!=len(entries): raise SystemExit('Duplicate qualified Form identities.')
    return path,entries,original_hash
desktop=root/'project_fo_3'
ns={'m':'http://schemas.microsoft.com/developer/msbuild/2003'}
project_path=desktop/'project_fo_3.csproj'
project_bytes=project_path.read_bytes()
project=ET.ElementTree(ET.fromstring(project_bytes))
files=[]
for node in project.findall('.//m:Compile',ns):
    source=desktop/node.attrib['Include'].replace('\\','/')
    if not source.is_file(): raise SystemExit('Compiled source missing: '+str(source))
    files.append(source)
assert files, 'No compiled files found'
source_bytes={p:p.read_bytes() for p in files}
texts={p:content.decode('utf-8-sig') for p,content in source_bytes.items()}
classes={}
for p,t in texts.items():
    for match in re.finditer(r'\bclass\s+(\w+)\s*:\s*([^\r\n{]+)',t):
        name,base=match.group(1),match.group(2).split(',')[0].strip()
        classes[name]=base
forms=set()
known={'Form','System.Windows.Forms.Form','AntdUI.Window','AntdUI.BaseForm'}
while True:
    additions={name for name,base in classes.items() if base in known or base.split('.')[-1] in forms}-forms
    if not additions:break
    forms.update(additions)
assert forms, 'No forms resolved'

assembly=None
assembly_hash=None
if args.assembly:
    assembly,entries,assembly_hash=read_form_metadata(args.assembly)
else:
    entries=[{'Class':name,'QualifiedClass':name,'Base':classes[name],'BaseFullName':classes[name],
              'SourceOwner':name,'SurfaceKind':'SourceHint','IsNested':None,'Constructors':[]} for name in forms]
tests=(desktop/'ResponsiveLayoutSelfTests.cs').read_text(encoding='utf-8-sig')
rows=[]
for entry in sorted(entries,key=lambda value:value['QualifiedClass']):
    name=entry['Class']
    owner=entry['SourceOwner'].split('`',1)[0]
    sources=[p for p,t in texts.items() if re.search(r'\bclass\s+'+re.escape(owner)+r'\b',t)]
    if not sources: raise SystemExit('No compiled source mapping for Form: '+entry['QualifiedClass'])
    combined='\n'.join(texts[p] for p in sources)
    rows.append({
        'Class':name,'QualifiedClass':entry['QualifiedClass'],'Base':entry['Base'],
        'BaseFullName':entry['BaseFullName'],'SurfaceKind':entry['SurfaceKind'],'IsNested':entry['IsNested'],
        'Constructors':'; '.join(entry['Constructors']),
        'CompiledSources':'; '.join(str(p.relative_to(root)).replace('\\','/') for p in sources),
        'AntdUIHintAtFileLevel':bool(re.search(r'\bAntdUI\.|DesktopComponents\.',combined)),
        'NativeInputHintAtFileLevel':'; '.join(sorted(set(re.findall(r'\b(TextBox|ComboBox|DataGridView|NumericUpDown)\b',combined)))),
        'SqlHintAtFileLevel':bool(re.search(r'\bSql(Connection|Command|DataAdapter|DataReader)\b|\b(?:SELECT|INSERT\s+INTO|UPDATE|DELETE\s+FROM)\b',combined)),
        'ThemeHintAtFileLevel':'UiTheme.' in combined,
        'DirectionHintAtFileLevel':bool(re.search(r'UiDirection|RightToLeft',combined)),
        'ResponsiveTestsMention':bool(re.search(r'\b'+re.escape(name)+r'\b',tests)),
        'LiveAcceptance':'NOT ASSESSED by inventory - retain existing per-screen evidence'
    })
dialogs=[]
dynamic_forms=[]
pattern=re.compile(r'\b(MessageBox\.Show|new\s+(?:OpenFileDialog|SaveFileDialog|PrintDialog|PrintPreviewDialog|PageSetupDialog|FontDialog|ColorDialog|FolderBrowserDialog))\b')
for p,t in texts.items():
    for lineNumber,line in enumerate(t.splitlines(),1):
        for match in pattern.finditer(line):
            dialogs.append({'Source':str(p.relative_to(root)).replace('\\','/'),'Line':lineNumber,'DialogCall':match.group(1)})
        if re.search(r'\bnew\s+(?:System\.Windows\.Forms\.)?Form\b',line):
            dynamic_forms.append({'Source':str(p.relative_to(root)).replace('\\','/'),'Line':lineNumber,
                                  'Call':'new Form','TestFileHint':bool(re.search(r'(?:SelfTests|Tests)\.cs$',p.name))})
if project_path.read_bytes()!=project_bytes: raise SystemExit('Project changed during inventory; rerun against stable inputs.')
for path,content in source_bytes.items():
    if path.read_bytes()!=content: raise SystemExit('Source changed during inventory: '+str(path))
if assembly and hashlib.sha256(assembly.read_bytes()).hexdigest().upper()!=assembly_hash:
    raise SystemExit('Assembly changed during inventory; rerun against a stable build.')
out.mkdir(parents=True)
for filename,data in [('screens.csv',rows),('native-dialog-calls.csv',dialogs),('dynamic-form-calls.csv',dynamic_forms)]:
    assert data
    with (out/filename).open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.DictWriter(f,fieldnames=list(data[0]));writer.writeheader();writer.writerows(data)
source_hashes=[{'Source':str(p.relative_to(root)).replace('\\','/'),'SHA256':hashlib.sha256(source_bytes[p]).hexdigest().upper()} for p in files]
with (out/'source-fingerprints.csv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=['Source','SHA256']);writer.writeheader();writer.writerows(source_hashes)
summary={'GeneratedUtc':generated.isoformat(),'Repository':str(root),
         'OutputDirectory':str(out),
         'InventoryMode':'compiled-type-metadata' if assembly else 'source-hints',
         'Assembly':str(assembly) if assembly else None,
         'AssemblySHA256':assembly_hash,
         'ProjectSHA256':hashlib.sha256(project_bytes).hexdigest().upper(),
         'CompiledSourceCount':len(files),'ResolvedFormCount':len(rows),
         'ApplicationWindowCount':sum(row['SurfaceKind']=='ApplicationWindow' for row in rows),
         'AbstractSurfaceCount':sum(row['SurfaceKind']=='AbstractSurface' for row in rows),
         'TestFixtureCount':sum(row['SurfaceKind']=='TestFixture' for row in rows),
         'SimpleNameCollisionCount':sum(count>1 for count in Counter(row['Class'] for row in rows).values()),
         'AntdUIFileHintCount':sum(row['AntdUIHintAtFileLevel'] for row in rows),
         'SqlFileHintCount':sum(row['SqlHintAtFileLevel'] for row in rows),
         'ResponsiveTestMentionCount':sum(row['ResponsiveTestsMention'] for row in rows),
         'NativeDialogCallCount':len(dialogs),
         'DynamicFormCallCount':len(dynamic_forms),
         'VerificationScope':'Compiled source list and optional assembly type metadata only; no window construction, entry-point execution, SQL or live acceptance. Styling hints are file level, not migration completion.'}
(out/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
table='\n'.join('| '+r['QualifiedClass']+' | '+r['SurfaceKind']+' | '+r['CompiledSources'].replace('; ','<br>')+' | '+('نعم' if r['AntdUIHintAtFileLevel'] else 'لا')+' | '+('مراجعة' if r['SqlHintAtFileLevel'] else 'لا إشارة')+' | غير مقيّم بهذا الجرد |' for r in rows)
(out/'README_AR.md').write_text(f'''# جرد الشاشات المضمنة في البناء — {generated:%Y-%m-%d} UTC

المصدر هو عناصر Compile في project_fo_3.csproj؛ أُهملت النسخ القديمة غير المضمنة في البناء. وضع الجرد: `{summary['InventoryMode']}`. عند تقديم assembly تُقرأ أنواع Form ووراثتها وأسماء أصحاب النوافذ المتداخلة من بيانات البناء الوصفية، دون إنشاء نافذة أو تشغيل Main. فُحصت {len(files)} ملفات مصدر ووجدت {len(rows)} فئة نافذة، و{len(dialogs)} موضع استدعاء لحوارات Windows القياسية. الملفات التفصيلية screens.csv وnative-dialog-calls.csv وsummary.json وsource-fingerprints.csv. النتائج على C ويُرفض تجاوز دليل جرد موجود.

في وضع بيانات البناء: {summary['ApplicationWindowCount']} فئة نافذة تطبيق مسماة، و{summary['AbstractSurfaceCount']} سطح وراثة مجرد، و{summary['TestFixtureCount']} نافذة اختبار. يوجد {summary['SimpleNameCollisionCount']} اسم قصير مشترك؛ الهوية الكاملة تفصل نوافذ الاختبار المتداخلة المتشابهة. صُنفت الفئات التابعة لـ Tests/SelfTests وUiAutomationPreview كاختبارات، مع بقاء الأدوات والحوارات المسماة ضمن ApplicationWindow. في وضع source-hints وحده لا يُستنتج عدد نوافذ التطبيق من هذه الفئات. تصنيف ApplicationWindow ليس تصنيفًا لشاشة أعمال أو إثباتًا للقبول. الجرد لا يلغي أدلة القبول السابقة ولا يشطب المطالب المفتوحة.

توجد {summary['DynamicFormCallCount']} إشارة نصية إلى إنشاء Form مباشرة؛ مواقعها محفوظة في dynamic-form-calls.csv لأنها لا تظهر كفئات مستقلة في بيانات البناء. قد تشمل مضيفات اختبار أو تعليقات، ولذلك تحتاج مراجعة مصدر منفصلة، ولا تُجمع تلقائيًا مع عدد شاشات التطبيق. قراءة assembly تستخدم ReflectionOnly ولا تنفذ كود التطبيق أو منشئات النوافذ.

الإشارات أدناه تخص ملفات الفئة، وقد يشترك ملف في أكثر من نافذة. وجود AntdUI أو ذكر الفئة في الاختبارات لا يثبت اعتماد جميع مكوناتها أو اجتياز العربية والتفاعل وDPI. إشارة SQL تستدعي مراجعة يدوية وليست حكمًا بوجود استعلام تنفيذي في كل نافذة. الحوارات المولدة ديناميكيًا وحالاتها التشغيلية تحتاج قبولًا حيًا منفصلًا.

| هوية النافذة | النوع | الملفات المضمنة | إشارة AntdUI أو مكونات مشتركة | إشارة SQL على مستوى الملف | القبول الحي |
|---|---|---|---|---|---|
{table}
''',encoding='utf-8')
print(json.dumps(summary,ensure_ascii=False))
