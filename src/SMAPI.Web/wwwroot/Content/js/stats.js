var smapi = smapi || {};
var app;
smapi.statsPage = function (options) {
    /*********
    ** Configure
    *********/
    const config = {
        // default colors for chart series (which cycle through the list)
        colorPalette: [
            "#117733", // dark green
            "#332288", // dark indigo
            "#88ccee", // azure
            "#cc6677", // muted rose
            "#ddcc77", // gold
            "#44aa99", // turquoise
            "#aa4499"  // magenta
        ],

        // configure the 'mods by type' stats
        modsByType: {
            // display names for each mod type (omitted mod types are shown as-is)
            labels: {
                "SMAPI": "SMAPI (C#)",
                "Pathoschild.ContentPatcher": "Content Patcher",
                "XNB": "XNB",
                "PeacefulEnd.AlternativeTextures": "Alternative Textures",
                "spacechase0.JsonAssets": "Json Assets",
                "PeacefulEnd.FashionSense": "Fashion Sense",
                "Digus.ProducerFrameworkMod": "Producer Framework Mod",
                "DIGUS.MailFrameworkMod": "Mail Framework Mod",
                "Esca.FarmTypeManager": "Farm Type Manager",
                "Cherry.ShopTileFramework": "Shop Tile Framework",
                "Platonymous.TMXLoader": "TMXL",
                "cat.betterartisangoodicons": "Better Artisan Good Icons",
                "spacechase0.DynamicGameAssets": "Dynamic Game Assets",
                "Platonymous.CustomFurniture": "Custom Furniture",
                "Platonymous.CustomMusic": "Custom Music",
                "leroymilo.FurnitureFramework": "Furniture Framework",
                "Paritee.BetterFarmAnimalVariety": "Better Farm Animal Variety",
                "other": "other (<100 packs)"
            },

            // colors for each mod type (omitted mod types cycle through the colorPalette)
            colors: {
                "other": "#c0c0c8" // silver-gray
            }
        }
    };


    /*********
    ** Set up state
    *********/
    const data = {
        modsByType: {
            rows: [],
            lastRow: {},
            xLabels: [],
            modTypes: []
        },
        newMods: {
            deltas: [],
            xLabels: []
        }
    };


    /*********
    ** Init app
    *********/
    app = new Vue({
        el: "#app",
        data: {
            isLoaded: false,
            loadFailed: false,

            modsByType: {
                previouslyUpdated: null,
                lastUpdated: null,
                summary: {
                    total: 0,
                    topThree: [],
                    xnbPercent: "0"
                }
            },
            latestNewMods: {
                total: 0,
                byType: [],
                forOther: 0
            }
        },
        mounted: function () {
            this.loadAsync();
        },
        methods: {
            /**
             * Fetch the raw data files, initialize the dataset, and build the page's charts.
             */
            loadAsync: async function () {
                /*********
                ** Fetch mods by type
                *********/
                {
                    // fetch dataset
                    const rows = await fetchJsonLinesAsync(options.modsByTypeUri);
                    if (!rows) {
                        this.loadFailed = true;
                        return;
                    }

                    // derive dataset info
                    const lastRow = rows[rows.length - 1];
                    data.modsByType = {
                        previouslyUpdated: formatFullDate(rows[rows.length - 2].date),
                        lastUpdated: formatFullDate(lastRow.date),

                        rows,
                        lastRow,
                        xLabels: rows.map(row => row.date.slice(0, 7)),
                        modTypes: getModTypes(rows)
                    };
                    this.modsByType.previouslyUpdated = data.modsByType.previouslyUpdated;
                    this.modsByType.lastUpdated = data.modsByType.lastUpdated;

                    // summarize mods by type
                    {
                        const curData = data.modsByType;
                        const total = curData.modTypes.reduce((sum, modType) => sum + (curData.lastRow[modType] ?? 0), 0);

                        const topThree = curData.modTypes
                            .filter(isSpecificModType)
                            .slice(0, 3)
                            .map(modType => ({ label: getLabel(config.modsByType.labels, modType), percent: getPercent(curData.lastRow[modType], total) }));

                        this.modsByType.summary = {
                            total,
                            topThree,
                            xnbPercent: getPercent(curData.lastRow["XNB"], total)
                        };
                    }

                    // assign colors for each mod type
                    let index = 0;
                    for (const modType of data.modsByType.modTypes) {
                        if (config.modsByType.colors[modType])
                            continue; // assigned in config

                        config.modsByType.colors[modType] = config.colorPalette[index++ % config.colorPalette.length];
                    }
                }

                /*********
                ** Calculate 'new mods this month' stats
                *********/
                {
                    const curData = data.modsByType;

                    // exclude historical manual adjustments (e.g. from mod dump rebuilds)
                    const excludeDates = new Set(["2021-03-06", "2022-01-01", "2023-05-04", "2024-08-02"]);
                    const excludeRow = row => excludeDates.has(row.date);

                    // get total new mods per month (skip first row which has no delta)
                    const totals = curData.rows.map(row => curData.modTypes.reduce((curTotal, modType) => curTotal + (row[modType] ?? 0), 0));
                    const deltas = curData.rows
                        .slice(1)
                        .map((_, i) => totals[i + 1] - totals[i])
                        .filter((_, i) => !excludeRow(curData.rows[i + 1]));
                    const filteredXLabels = curData.xLabels
                        .slice(1)
                        .filter((_, i) => !excludeRow(curData.rows[i + 1]));
                    const lastDelta = deltas[deltas.length - 1];

                    // get per-type deltas for last month
                    const lastRow = curData.lastRow;
                    const prevRow = curData.rows[curData.rows.length - 2];
                    const byType = curData.modTypes
                        .filter(isSpecificModType)
                        .map(modType => ({
                            type: modType,
                            label: getLabel(config.modsByType.labels, modType),
                            delta: (lastRow[modType] ?? 0) - (prevRow[modType] ?? 0)
                        }))
                        .sort((a, b) => b.delta - a.delta);
                    const otherDelta = (lastRow["other"] ?? 0) - (prevRow["other"] ?? 0);

                    // save values
                    data.newMods = {
                        deltas: deltas,
                        xLabels: filteredXLabels
                    };
                    this.latestNewMods = {
                        total: lastDelta,
                        byType: byType,
                        forOther: otherDelta
                    };
                }


                /*********
                ** Build charts
                *********/
                this.isLoaded = true;
                await this.$nextTick(); // wait for Vue to render chart containers

                const charts = [];
                const titleStyle = {
                    fontFamily: "Roboto",
                    fontSize: 13
                };

                /****
                ** 'Mods by type' pie chart
                ****/
                {
                    const curData = data.modsByType;
                    const countsByType = curData.lastRow; // counts from last row
                    const includeModTypes = curData.modTypes.filter(type => countsByType[type]); // ignore mod types which don't appear in the last row

                    const total = includeModTypes.reduce((sum, type) => sum + (countsByType[type] ?? 0), 0);

                    const chart = echarts.init(document.getElementById("modsByType"));

                    chart.setOption({
                        title: {
                            text: "Total mods by type",
                            textStyle: titleStyle
                        },
                        series: [
                            {
                                type: "pie",
                                radius: "80%",
                                clockwise: false, // start with larger values on the left
                                label: {
                                    formatter: entry => `${entry.name}  ${getPercent(entry.value, total)}%`
                                },
                                data: includeModTypes.map(modType => ({
                                    name: getLabel(config.modsByType.labels, modType),
                                    value: countsByType[modType] ?? 0,
                                    itemStyle: {
                                        color: getColor(config.modsByType.colors, modType)
                                    }
                                }))
                            }
                        ],
                        tooltip: {
                            formatter: entry => `${entry.name}: ${entry.value.toLocaleString()} (${getPercent(entry.value, total)}%)`
                        },
                        toolbox: createToolbox(false)
                    });

                    charts.push(chart);
                }

                /****
                ** 'Total mods by type' line charts
                ****/
                {
                    const curData = data.modsByType;
                    const chartConfigs = [
                        {
                            id: "modsByTypeOverTime",
                            title: "Mods by type over time",
                            modTypes: curData.modTypes.filter(isSpecificModType)
                        },
                        {
                            id: "modsByTypeOverTimeExcludingOutliers",
                            title: "Mods by type over time (excluding SMAPI and Content Patcher)",
                            modTypes: curData.modTypes.filter(k => isSpecificModType(k) && k !== "SMAPI" && k !== "Pathoschild.ContentPatcher")
                        }
                    ];

                    for (const chartConfig of chartConfigs) {
                        const chart = echarts.init(document.getElementById(chartConfig.id));

                        chart.setOption({
                            title: {
                                text: chartConfig.title,
                                textStyle: titleStyle
                            },
                            legend: {
                                show: false
                            },
                            grid: {
                                top: 40,
                                left: 60,
                                right: 150 // extra right margin to fit end labels
                            },
                            xAxis: {
                                data: curData.xLabels,
                                axisLabel: {
                                    rotate: 90,
                                    fontSize: 11
                                }
                            },
                            yAxis: {},
                            series: chartConfig.modTypes.map(modType => {
                                const extendsToEnd = !!curData.lastRow[modType];
                                const rawColor = getColor(config.modsByType.colors, modType);
                                const color = extendsToEnd
                                    ? rawColor
                                    : hexToRgba(rawColor, 0.6);

                                return {
                                    type: "line",
                                    name: getLabel(config.modsByType.labels, modType),
                                    data: curData.rows.map(row => row[modType] ?? null),
                                    color: color,
                                    lineStyle: {
                                        type: extendsToEnd ? "solid" : "dotted",
                                        width: 2
                                    },
                                    symbol: "none",
                                    endLabel: {
                                        show: true,
                                        formatter: "{a}", // {a} = name
                                        color: color
                                    },
                                    labelLayout: {
                                        moveOverlap: extendsToEnd
                                            ? "shiftY" // if the line reaches the end of the chart, use 'shiftY' layout to avoid overlapping labels
                                            : null     // if the line ends early, just display it next to the line instead
                                    }
                                };
                            }),
                            tooltip: {
                                trigger: "axis"
                            },
                            toolbox: createToolbox(true)
                        });

                        charts.push(chart);
                    }
                }

                /****
                ** 'New mods' pie chart
                ****/
                {
                    const countsByType = [
                        ...this.latestNewMods.byType,
                        {
                            type: "other",
                            label: getLabel(config.modsByType.labels, "other"),
                            delta: this.latestNewMods.forOther
                        }
                    ];

                    const chart = echarts.init(document.getElementById("newModsLastDelta"));

                    chart.setOption({
                        title: {
                            text: `New mods between ${data.modsByType.previouslyUpdated} and ${data.modsByType.lastUpdated}`,
                            textStyle: titleStyle
                        },
                        series: [
                            {
                                type: "pie",
                                radius: "80%",
                                clockwise: false, // start with larger values on the left
                                label: {
                                    formatter: entry => `${entry.name}  ${entry.percent.toFixed(1)}%`
                                },
                                data: countsByType
                                    .filter(entry => entry.delta > 0)
                                    .map(entry => ({
                                        name: entry.label,
                                        value: entry.delta,
                                        itemStyle: {
                                            color: getColor(config.modsByType.colors, entry.type)
                                        }
                                    }))
                            }
                        ],
                        tooltip: {
                            formatter: entry => `${entry.name}: ${entry.value.toLocaleString()} (${entry.percent.toFixed(1)}%)`
                        },
                        toolbox: createToolbox(false)
                    });

                    charts.push(chart);
                }

                /****
                ** 'New mods' bar chart
                ****/
                {
                    const chart = echarts.init(document.getElementById("newMods"));

                    chart.setOption({
                        title: {
                            text: "New mods by month",
                            textStyle: titleStyle
                        },
                        legend: {
                            show: false
                        },
                        grid: {
                            top: 40,
                            left: 60,
                            right: 60
                        },
                        xAxis: {
                            data: data.newMods.xLabels,
                            axisLabel: {
                                rotate: 90,
                                fontSize: 11
                            }
                        },
                        yAxis: {},
                        series: [
                            {
                                type: "bar",
                                data: data.newMods.deltas
                            }
                        ],
                        tooltip: {
                            trigger: "axis"
                        },
                        toolbox: createToolbox(true)
                    });
                    charts.push(chart);
                }

                window.addEventListener("resize", () => resizeCharts(charts));
            },

            /**
             * Format a numeric change for display, with comma separators and sign.
             * @param {number} value The number to format.
             * @returns {string} Returns the formatted number.
             */
            formatDelta: function (value) {
                return value > 0
                    ? `+${value.toLocaleString()}`
                    : value.toLocaleString();
            }
        }
    });


    /*********
    ** Helper methods
    *********/
    /****
    ** Generic data
    ****/
    /**
     * Fetch and parse data from a JSONL URI.
     * @param {string} fetchUri The URI from which to fetch the JSONL data.
     * @returns {null|array<object>} Returns an array of parsed objects if the data was found, else `null`.
     */
    async function fetchJsonLinesAsync(fetchUri) {
        const response = await fetch(fetchUri);
        if (!response.ok)
            return null;

        const rawJsonLines = await response.text();
        return rawJsonLines.trim().split(/\r?\n/).filter(row => !!row).map(JSON.parse);
    }

    /**
     * Get a formatted date string.
     * @param {Date|string} date The ISO 8601 date-only string to format.
     * @param {Intl.DateTimeFormatOptions} options The date format options.
     */
    function formatDate(date, options) {
        if (typeof date === "string")
            date = new Date(date + "T12:00"); // set time to noon to avoid any timezone conversions changing the date parts

        return new Intl.DateTimeFormat("en-GB", options).format(date);
    }

    /**
     * Get a formatted date string in the form 'dd MMM yyyy', like '31 January 2030'.
     * @param {Date|string} date The ISO 8601 date-only string to format.
     */
    function formatFullDate(date) {
        return formatDate(date, { dateStyle: "long" });
    }

    /**
     * Get the display name for a data key.
     * @param {object} lookup The name dictionary to check.
     * @param {string} key The data key.
     * @returns {string}
     */
    function getLabel(lookup, key) {
        return lookup[key] ?? key;
    }

    /**
     * Get the hexadecimal color code for a data key.
     * @param {object} lookup The color dictionary to check.
     * @param {string} key The data key.
     * @returns {string}
     */
    function getColor(lookup, key) {
        return lookup[key] ?? "#808080";
    }

    /**
     * Get a single-decimal percentage value.
     * @param {number|null|undefined} value The value for which to get a percentage.
     * @param {number} total The denominator for the value.
     * @returns {string}
     */
    function getPercent(value, total) {
        if (!value)
            value = 0;

        return (Math.round(value / total * 1000) / 10).toString();
    }

    /**
     * Get an RGBA representation of a hexadecimal color.
     * @param {string} hex The hexadecimal color code, including the '#' prefix.
     * @param {number} alpha The alpha channel, as a value between 0 (transparent) and 1 (opaque).
     * @returns
     */
    function hexToRgba(hex, alpha) {
        const r = parseInt(hex.slice(1, 3), 16);
        const g = parseInt(hex.slice(3, 5), 16);
        const b = parseInt(hex.slice(5, 7), 16);
        return `rgba(${r},${g},${b},${alpha})`;
    }

    /****
    ** Mods-by-type data
    ****/
    /**
     * Get the unique mod types, sorted by their number of mods in the latest row.
     * @param {array<object>} rows The parsed mods-by-type rows over time.
     * @returns {array<string>}
     */
    function getModTypes(rows) {
        const rawModTypes = rows.flatMap(row => Object.keys(row).filter(isModType));
        const uniqueModTypes = [...new Set(rawModTypes)];

        const lastRow = rows[rows.length - 1];
        return uniqueModTypes.sort((modTypeA, modTypeB) => {
            // special case: 'other' always at the end
            if (modTypeA === "other")
                return 1;
            if (modTypeB === "other")
                return -1;

            // else sort by count descending
            const countA = lastRow[modTypeA] ?? 0;
            const countB = lastRow[modTypeB] ?? 0;
            return countB - countA;
        })
    }

    /**
     * Get whether a key in a data row is a mod type, rather than a metadata field (like 'date' or '@comment').
     * @param {string} key The data key to check.
     */
    function isModType(key) {
        return key !== "date" && key !== "@comment";
    }

    /**
     * Get whether a key in a data row is a strict mod type, rather than a metadata field (like 'date' or '@comment') or 'other'.
     * @param {string} key The data key to check.
     */
    function isSpecificModType(key) {
        return key !== "date" && key !== "@comment" && key !== "other";
    }

    /****
    ** Charts
    ****/
    /**
     * Create the toolbox options for a chart.
     * @param {boolean} isLinearChart Whether the options are for a linear chart (e.g. a bar chart or line chart).
     */
    function createToolbox(isLinearChart) {
        const toolbox = {
            show: true,
            feature: {
                dataView: {
                    readOnly: true
                },
                saveAsImage: { }
            }
        };

        if (isLinearChart)
            toolbox.feature.dataZoom = {};

        return toolbox;
    }

    /**
     * Resize all charts to match their container size.
     * @param {array<object>} charts The charts to resize.
     */
    function resizeCharts(charts) {
        for (const chart of charts)
            chart.resize();
    }
};
