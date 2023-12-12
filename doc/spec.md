# List of tuple

<table>
<tr>
<td>

```mermaid
graph TD
  Start["List<(char,char)>"] --> tuple["(char,char)"]
  tuple --> char1["char"]
  tuple --> char2["char"]
```
</td>
<td>

```mermaid
graph TD
    A1["((.)(.))+"]
    A1 --> tuple["(.)(.)"]
    tuple --> char1["."]
    tuple --> char2["."]
```
</td>
</tr>
</table>

# List of primitive type

<table>
<tr>
<td>

```mermaid
graph TD
  Start["List#lt;int>"] --> int
```
</td>
<td>

```mermaid
graph TD
    A1["((\d+),? ?)+"]
    A1 --> int["(\d+),? ?"]
```
</td>
<td></td>
<td>

```mermaid
graph TD
  Start["List#lt;int>"] --> int
```
</td>
<td>

```mermaid
graph TD
    A1["(\d+ ?)+"]
    A1 --> int["\d+ ?"]
```
</td>
</tr>
</table>

# Dictionary of tuple including list

<table>
<tr>
<td>

```mermaid
graph TD
  Start["Dictionary#lt;int,List#lt;string>>"] --> tuple["(int,List#lt;string>)"]
  tuple --> int
  tuple --> list["List#lt;string>"]
  list --> string
```
</td>
<td>

```mermaid
graph TD
    A1["((\d+) = ((\w+),? ?)+)+"]
    A1 --> tuple["(\d+) = ((\w+),? ?)+"]
    tuple --> int["\d+"]
    tuple --> list["(\w+),? ?"]
    list --> string["\w+"]
```
</td>
</tr>
</table>

# Record with two values

<table>
<tr>
<td>

```mermaid
graph TD
  Start["record(long,long)"] --> long1[long]
  Start --> long2[long]
```
</td>
<td>

```mermaid
graph TD
    A1["mem\[(\d+)\] = (\d+)"]
    A1 --> long1["\d+"]
    A1 --> long2["\d+"]
```
</td>
</tr>
</table>

# List of List of List of char

<table>
<tr>
<td>

```mermaid
graph TD
  Start["List#lt;List#lt;List#lt;char>>>"] --> listlist["List#lt;List#lt;char>>"]
  listlist --> list["List#lt;char>"]
  list --> char  
```
</td>
<td>

```mermaid
graph TD
    A1["(((\w)+\s+)+,? ?)+"] --> listlist["((\w)+\s+)+,? ?"]
    listlist --> list["(\w)+\s+"]
    list --> char["\w"]
```
</td>
</tr>
</table>
