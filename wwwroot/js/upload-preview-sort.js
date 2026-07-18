(function (global, $) {
    'use strict';

    function compareValues(left, right) {
        var leftNum = Number(left.replace(/,/g, ''));
        var rightNum = Number(right.replace(/,/g, ''));
        var leftIsNum = left !== '' && !Number.isNaN(leftNum);
        var rightIsNum = right !== '' && !Number.isNaN(rightNum);

        if (leftIsNum && rightIsNum) {
            return leftNum - rightNum;
        }

        return left.localeCompare(right, undefined, { numeric: true, sensitivity: 'base' });
    }

    function cellSortValue($cell) {
        return String($cell.attr('data-sort') || $cell.text() || '')
            .replace(/\s+/g, ' ')
            .trim();
    }

    function clearSortState($table) {
        $table.find('thead tr').first().children('th')
            .removeClass('upload-preview-sort-asc upload-preview-sort-desc')
            .removeAttr('aria-sort');
    }

    function sortTable($table, columnIndex, direction) {
        var $tbody = $table.children('tbody');
        var rows = $tbody.children('tr').get();

        rows.sort(function (a, b) {
            var left = cellSortValue($(a).children('td').eq(columnIndex));
            var right = cellSortValue($(b).children('td').eq(columnIndex));
            var result = compareValues(left, right);
            return direction === 'desc' ? -result : result;
        });

        $tbody.append(rows);
    }

    function bind($root) {
        var $tables = $($root).find('table.upload-preview-sortable');
        if (!$tables.length) {
            return;
        }

        $tables.each(function () {
            var $table = $(this);
            var $headers = $table.find('thead tr').first().children('th');

            $headers.each(function (index) {
                var $th = $(this);
                if ($th.hasClass('upload-preview-sort-bound')) {
                    return;
                }

                $th.addClass('upload-preview-sort-bound upload-preview-sortable-th')
                    .attr('role', 'button')
                    .attr('tabindex', '0')
                    .on('click.uploadPreviewSort keydown.uploadPreviewSort', function (event) {
                        if (event.type === 'keydown' && event.key !== 'Enter' && event.key !== ' ') {
                            return;
                        }

                        event.preventDefault();
                        var current = $th.hasClass('upload-preview-sort-asc')
                            ? 'asc'
                            : ($th.hasClass('upload-preview-sort-desc') ? 'desc' : '');
                        var next = current === 'asc' ? 'desc' : 'asc';

                        clearSortState($table);
                        $th.addClass(next === 'asc' ? 'upload-preview-sort-asc' : 'upload-preview-sort-desc')
                            .attr('aria-sort', next === 'asc' ? 'ascending' : 'descending');
                        sortTable($table, index, next);
                    });
            });
        });
    }

    global.UploadPreviewSort = {
        bind: bind
    };
})(window, jQuery);
